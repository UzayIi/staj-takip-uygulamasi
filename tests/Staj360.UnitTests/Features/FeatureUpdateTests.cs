using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.TeamWork;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Configuration;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.Infrastructure.Services;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Features;

public class FeatureUpdateTests
{
    [Fact]
    public async Task CreateIntern_Persists_Address()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var unit = new OrganizationUnit
            {
                Code = "BILGI_TEKNOLOJILERI",
                Name = "Bilgi Teknolojileri Şube Müdürlüğü",
                UnitType = OrganizationUnitType.Branch,
                DisplayOrder = 10,
                IsActive = true
            };
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = "adres.test@staj360.local",
                Email = "adres.test@staj360.local",
                FullName = "Adres Test",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = utc
            };
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(user, "Test!12345")).Succeeded);
            await users.AddToRoleAsync(user, AppRoles.Intern);

            db.InternProfiles.Add(new InternProfile
            {
                UserId = user.Id,
                StudentNumber = "ADR-1",
                CurrentOrganizationUnitId = unit.Id,
                University = "Üni",
                Faculty = "Fak",
                SchoolDepartment = "Bölüm",
                ClassLevel = "3",
                PhoneNumber = "05001112233",
                Address = "Diyarbakır Merkez Mah. No:1",
                IsActive = true
            });
            await db.SaveChangesAsync();

            var profile = await db.InternProfiles.SingleAsync(p => p.StudentNumber == "ADR-1");
            Assert.Equal("Diyarbakır Merkez Mah. No:1", profile.Address);
            Assert.Equal(unit.Id, profile.CurrentOrganizationUnitId);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task OrganizationUnit_Is_ReadOnly_Catalog_Seeded()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await SeedOrganizationCatalogAsync(db);

            var branch = await db.OrganizationUnits.SingleAsync(u => u.Code == OrganizationSeedCatalog.DefaultBranchCode);
            Assert.True(branch.IsActive);
            Assert.Equal(OrganizationUnitType.Branch, branch.UnitType);

            // Katalog birimleri CRUD ile silinmez; soft-delete yapılmamalı.
            Assert.False(branch.IsDeleted);
            Assert.True(await db.OrganizationUnits.CountAsync() > 10);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task TeamWork_Hides_Draft_Reports_And_Personal_Fields()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var team = sp.GetRequiredService<ITeamWorkService>();

            var unit = new OrganizationUnit
            {
                Code = "BILGI_TEKNOLOJILERI",
                Name = "Bilgi Teknolojileri Şube Müdürlüğü",
                UnitType = OrganizationUnitType.Branch,
                DisplayOrder = 10,
                IsActive = true
            };
            db.OrganizationUnits.Add(unit);
            var schedule = new WorkSchedule
            {
                Name = "Mesai", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0),
                GracePeriodMinutes = 15, MondayEnabled = true, TuesdayEnabled = true,
                WednesdayEnabled = true, ThursdayEnabled = true, FridayEnabled = true
            };
            db.WorkSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var mentor = new ApplicationUser
            {
                UserName = "m.team@test.local", Email = "m.team@test.local", FullName = "Mentor",
                IsActive = true, EmailConfirmed = true, CreatedAtUtc = utc
            };
            Assert.True((await users.CreateAsync(mentor, "Test!12345")).Succeeded);

            async Task<Guid> AddIntern(string email, string name, string no)
            {
                var u = new ApplicationUser
                {
                    UserName = email, Email = email, FullName = name,
                    IsActive = true, EmailConfirmed = true, CreatedAtUtc = utc
                };
                Assert.True((await users.CreateAsync(u, "Test!12345")).Succeeded);
                await users.AddToRoleAsync(u, AppRoles.Intern);
                var p = new InternProfile
                {
                    UserId = u.Id, StudentNumber = no, CurrentOrganizationUnitId = unit.Id, IsActive = true,
                    Address = "Gizli Adres", PhoneNumber = "0555"
                };
                // Address/Phone should never appear in team DTO
                db.InternProfiles.Add(p);
                await db.SaveChangesAsync();
                db.InternshipPeriods.Add(new InternshipPeriod
                {
                    InternProfileId = p.Id, MentorUserId = mentor.Id, WorkScheduleId = schedule.Id,
                    StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 8, 31),
                    RequiredWorkDays = 20, Status = InternshipStatus.Active
                });
                await db.SaveChangesAsync();
                return p.Id;
            }

            var aId = await AddIntern("a.team@test.local", "Ali Veli", "TW-1");
            var periodId = await db.InternshipPeriods.Where(p => p.InternProfileId == aId).Select(p => p.Id).SingleAsync();

            db.DailyReports.Add(new DailyReport
            {
                InternshipPeriodId = periodId, ReportDate = new DateOnly(2026, 7, 10),
                Status = DailyReportStatus.Draft, GeneralNotes = "Taslak gizli"
            });
            db.DailyReports.Add(new DailyReport
            {
                InternshipPeriodId = periodId, ReportDate = new DateOnly(2026, 7, 11),
                Status = DailyReportStatus.Submitted, GeneralNotes = "Gönderilmiş iş",
                WorkItems = { new DailyWorkItem { Title = "API geliştirmesi", DurationMinutes = 120 } }
            });
            await db.SaveChangesAsync();

            var reports = await team.ListPeerReportsAsync(new TeamReportFilter(null, null, null, null, null), 1, 20);
            Assert.Single(reports.Items);
            Assert.Equal(DailyReportStatus.Submitted, reports.Items[0].Status);
            Assert.Contains("API", reports.Items[0].WorkSummary);
            Assert.DoesNotContain("Gizli", reports.Items[0].WorkSummary);
            Assert.DoesNotContain("0555", reports.Items[0].InternFullName);
            Assert.Equal("Ali Veli", reports.Items[0].InternFullName);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    private static async Task SeedOrganizationCatalogAsync(AppDbContext db)
    {
        if (await db.OrganizationUnits.AnyAsync())
            return;

        var byCode = new Dictionary<string, OrganizationUnit>(StringComparer.Ordinal);
        foreach (var def in OrganizationSeedCatalog.All.Where(d => d.ParentCode is null))
        {
            var unit = new OrganizationUnit
            {
                Code = def.Code,
                Name = def.Name,
                UnitType = def.Type,
                DisplayOrder = def.DisplayOrder,
                IsActive = true
            };
            db.OrganizationUnits.Add(unit);
            byCode[def.Code] = unit;
        }
        await db.SaveChangesAsync();

        foreach (var def in OrganizationSeedCatalog.All.Where(d => d.ParentCode is not null))
        {
            db.OrganizationUnits.Add(new OrganizationUnit
            {
                Code = def.Code,
                Name = def.Name,
                UnitType = def.Type,
                ParentId = byCode[def.ParentCode!].Id,
                DisplayOrder = def.DisplayOrder,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ServiceProvider> CreateHostAsync(DateTime utc)
    {
        var dbName = "Staj360Feature_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new TestClock(utc));
        services.AddSingleton<ITimeZoneService>(new FixedTimeZoneService());
        services.AddSingleton<IAuditLogService, NoOpAuditLogService>();
        services.AddSingleton(Options.Create(new OrganizationOptions { Name = "Test", BrandName = "StajAmed", TimeZone = "Europe/Istanbul" }));
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer($"Server=localhost\\SQLEXPRESS01;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<ITeamWorkService, TeamWorkService>();

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new ApplicationRole(role));
        }
        return sp;
    }
}
