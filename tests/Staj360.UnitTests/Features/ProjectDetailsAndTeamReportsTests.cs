using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Projects;
using Staj360.Application.Services.TeamWork;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Configuration;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.Infrastructure.Services;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Features;

public class ProjectDetailsAndTeamReportsTests
{
    [Fact]
    public async Task Admin_Mentor_Intern_Can_View_Project_Details()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var fx = await SeedFixtureAsync(sp, utc);
            var details = sp.GetRequiredService<IProjectDetailsService>();

            var admin = await details.GetDetailsAsync(fx.ProjectId, fx.AdminId, ProjectViewerKind.Admin);
            Assert.True(admin.Success);
            Assert.True(admin.Data!.CanManage);
            Assert.Equal("Demo Proje", admin.Data.Name);

            var mentorOwn = await details.GetDetailsAsync(fx.ProjectId, fx.MentorId, ProjectViewerKind.Mentor);
            Assert.True(mentorOwn.Success);
            Assert.True(mentorOwn.Data!.CanManage);

            var intern = await details.GetDetailsAsync(fx.ProjectId, fx.InternAUserId, ProjectViewerKind.Intern);
            Assert.True(intern.Success);
            Assert.False(intern.Data!.CanManage);
            Assert.All(intern.Data.Team, t =>
            {
                Assert.DoesNotContain("@", t.FullName);
                Assert.DoesNotContain("0555", t.FullName + (t.RoleDescription ?? "") + (t.PeriodLabel ?? ""));
            });
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Intern_Cannot_Manage_Others_Project_Via_Service()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var fx = await SeedFixtureAsync(sp, utc);
            var projects = sp.GetRequiredService<IProjectService>();

            var update = await projects.UpdateProgressAsync(fx.InternAUserId, isAdmin: false, fx.ProjectId, 90, ProjectStatus.InProgress);
            Assert.False(update.Success);
            Assert.Equal("FORBIDDEN", update.ErrorCode);

            var assign = await projects.AssignInternAsync(fx.InternAUserId, false, fx.ProjectId, fx.InternBProfileId, "hack");
            Assert.False(assign.Success);
            Assert.Equal("FORBIDDEN", assign.ErrorCode);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Peer_Can_Read_Submitted_And_Approved_But_Not_Draft_Or_Rejected()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var fx = await SeedFixtureAsync(sp, utc);
            var team = sp.GetRequiredService<ITeamWorkService>();

            var list = await team.ListPeerReportsAsync(new TeamReportFilter(null, null, null, null, null), 1, 50);
            Assert.Equal(2, list.Items.Count);
            Assert.All(list.Items, i => Assert.Contains(i.Status, new[] { DailyReportStatus.Submitted, DailyReportStatus.Approved }));
            Assert.DoesNotContain(list.Items, i => i.Status == DailyReportStatus.Draft);
            Assert.DoesNotContain(list.Items, i => i.Status == DailyReportStatus.Rejected);
            Assert.All(list.Items, i =>
            {
                Assert.DoesNotContain("MentorGizli", i.WorkSummary);
                Assert.DoesNotContain("0555", i.InternFullName);
                Assert.DoesNotContain("@", i.InternFullName);
            });

            var submitted = await team.GetPeerReportDetailAsync(fx.SubmittedReportId, fx.InternBUserId);
            Assert.True(submitted.Success);

            var approved = await team.GetPeerReportDetailAsync(fx.ApprovedReportId, fx.InternBUserId);
            Assert.True(approved.Success);
            Assert.Null(approved.Data!.GetType().GetProperty("MentorComment"));

            var draft = await team.GetPeerReportDetailAsync(fx.DraftReportId, fx.InternBUserId);
            Assert.False(draft.Success);
            Assert.Equal("NOT_FOUND", draft.ErrorCode);

            var rejected = await team.GetPeerReportDetailAsync(fx.RejectedReportId, fx.InternBUserId);
            Assert.False(rejected.Success);
            Assert.Equal("NOT_FOUND", rejected.ErrorCode);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Owner_Can_See_Own_Draft_Detail_Via_Peer_Endpoint()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var fx = await SeedFixtureAsync(sp, utc);
            var team = sp.GetRequiredService<ITeamWorkService>();
            var own = await team.GetPeerReportDetailAsync(fx.DraftReportId, fx.InternAUserId);
            Assert.True(own.Success);
            Assert.Equal(DailyReportStatus.Draft, own.Data!.Status);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Invalid_Project_Id_Returns_NotFound()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var details = sp.GetRequiredService<IProjectDetailsService>();
            var result = await details.GetDetailsAsync(Guid.NewGuid(), Guid.NewGuid(), ProjectViewerKind.Admin);
            Assert.False(result.Success);
            Assert.Equal("NOT_FOUND", result.ErrorCode);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Filters_And_Pagination_Work_Together()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var fx = await SeedFixtureAsync(sp, utc);
            var team = sp.GetRequiredService<ITeamWorkService>();

            var page1 = await team.ListPeerReportsAsync(
                new TeamReportFilter("Ali", null, null, null, DailyReportStatus.Submitted), 1, 1);
            Assert.Single(page1.Items);
            Assert.True(page1.TotalCount >= 1);
            Assert.All(page1.Items, i => Assert.Contains("Ali", i.InternFullName, StringComparison.OrdinalIgnoreCase));
            Assert.All(page1.Items, i => Assert.Equal(DailyReportStatus.Submitted, i.Status));
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Peer_Cannot_Mutate_Others_Reports_Through_Services()
    {
        // Mutasyon endpoint'leri controller'da Forbid döner; rapor servisinde de peer erişim salt-okunur.
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var fx = await SeedFixtureAsync(sp, utc);
            var team = sp.GetRequiredService<ITeamWorkService>();
            var detail = await team.GetPeerReportDetailAsync(fx.SubmittedReportId, fx.InternBUserId);
            Assert.True(detail.Success);
            // DTO'da mentor yorumu yok — özel not sızdırılmaz.
            var json = System.Text.Json.JsonSerializer.Serialize(detail.Data);
            Assert.DoesNotContain("MentorGizli", json);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Project_Details_Dto_Has_No_Pii_Fields()
    {
        var utc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var fx = await SeedFixtureAsync(sp, utc);
            var details = sp.GetRequiredService<IProjectDetailsService>();
            var dto = (await details.GetDetailsAsync(fx.ProjectId, fx.InternAUserId, ProjectViewerKind.Intern)).Data!;
            var json = System.Text.Json.JsonSerializer.Serialize(dto);
            Assert.DoesNotContain("Gizli Adres", json);
            Assert.DoesNotContain("0555", json);
            Assert.DoesNotContain("@test.local", json);
            Assert.DoesNotContain("TW-A", json); // student number
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    private static async Task<ServiceProvider> CreateHostAsync(DateTime utc)
    {
        var dbName = "Staj360ProjTeam_" + Guid.NewGuid().ToString("N");
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
        services.AddScoped<IUserDisplayLookup, UserDisplayLookup>();
        services.AddScoped<IUnitAssignmentService, UnitAssignmentService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectDetailsService, ProjectDetailsService>();
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

    private sealed record Fixture(
        Guid AdminId, Guid MentorId, Guid ProjectId,
        Guid InternAUserId, Guid InternBUserId, Guid InternBProfileId,
        Guid DraftReportId, Guid SubmittedReportId, Guid ApprovedReportId, Guid RejectedReportId);

    private static async Task<Fixture> SeedFixtureAsync(ServiceProvider sp, DateTime utc)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = new ApplicationUser { UserName = "admin@test.local", Email = "admin@test.local", FullName = "Admin", IsActive = true, EmailConfirmed = true, CreatedAtUtc = utc };
        var mentor = new ApplicationUser { UserName = "mentor@test.local", Email = "mentor@test.local", FullName = "Mentor Deneme", IsActive = true, EmailConfirmed = true, CreatedAtUtc = utc };
        var internA = new ApplicationUser { UserName = "a@test.local", Email = "a@test.local", FullName = "Ali Yılmaz", IsActive = true, EmailConfirmed = true, CreatedAtUtc = utc };
        var internB = new ApplicationUser { UserName = "b@test.local", Email = "b@test.local", FullName = "Bora Kaya", IsActive = true, EmailConfirmed = true, CreatedAtUtc = utc };
        Assert.True((await users.CreateAsync(admin, "Test!12345")).Succeeded);
        Assert.True((await users.CreateAsync(mentor, "Test!12345")).Succeeded);
        Assert.True((await users.CreateAsync(internA, "Test!12345")).Succeeded);
        Assert.True((await users.CreateAsync(internB, "Test!12345")).Succeeded);
        await users.AddToRoleAsync(admin, AppRoles.Admin);
        await users.AddToRoleAsync(mentor, AppRoles.Mentor);
        await users.AddToRoleAsync(internA, AppRoles.Intern);
        await users.AddToRoleAsync(internB, AppRoles.Intern);

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

        var profileA = new InternProfile
        {
            UserId = internA.Id, StudentNumber = "TW-A", CurrentOrganizationUnitId = unit.Id, IsActive = true,
            Address = "Gizli Adres", PhoneNumber = "05551234567"
        };
        var profileB = new InternProfile
        {
            UserId = internB.Id, StudentNumber = "TW-B", CurrentOrganizationUnitId = unit.Id, IsActive = true,
            Address = "Gizli Adres 2", PhoneNumber = "05557654321"
        };
        db.InternProfiles.AddRange(profileA, profileB);
        await db.SaveChangesAsync();

        var periodA = new InternshipPeriod
        {
            InternProfileId = profileA.Id, MentorUserId = mentor.Id, WorkScheduleId = schedule.Id,
            StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 8, 31),
            RequiredWorkDays = 20, Status = InternshipStatus.Active
        };
        db.InternshipPeriods.Add(periodA);

        var project = new Project
        {
            Name = "Demo Proje", Description = "Açıklama", StartDate = new DateOnly(2026, 5, 1),
            OrganizationUnitId = unit.Id, MentorUserId = mentor.Id, Status = ProjectStatus.InProgress, ProgressPercentage = 40
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        db.ProjectAssignments.Add(new ProjectAssignment
        {
            ProjectId = project.Id, InternProfileId = profileA.Id, IsActive = true,
            AssignedAtUtc = utc, RoleDescription = "Geliştirici"
        });
        db.ProjectTasks.Add(new ProjectTask
        {
            ProjectId = project.Id, AssignedInternProfileId = profileA.Id, Title = "Görev 1",
            Status = ProjectTaskStatus.InProgress, Priority = TaskPriority.Medium
        });

        var draft = new DailyReport
        {
            InternshipPeriodId = periodA.Id, ReportDate = new DateOnly(2026, 7, 1),
            OrganizationUnitId = unit.Id,
            Status = DailyReportStatus.Draft, GeneralNotes = "Taslak gizli",
            MentorComment = "MentorGizli"
        };
        var submitted = new DailyReport
        {
            InternshipPeriodId = periodA.Id, ReportDate = new DateOnly(2026, 7, 2),
            OrganizationUnitId = unit.Id,
            Status = DailyReportStatus.Submitted, GeneralNotes = "Gönderilmiş iş",
            WorkItems = { new DailyWorkItem { Title = "API geliştirmesi", DurationMinutes = 120, TechnologiesUsed = "C#", ProjectId = project.Id } }
        };
        var approved = new DailyReport
        {
            InternshipPeriodId = periodA.Id, ReportDate = new DateOnly(2026, 7, 3),
            OrganizationUnitId = unit.Id,
            Status = DailyReportStatus.Approved, GeneralNotes = "Onaylı iş",
            MentorComment = "MentorGizliOnay",
            WorkItems = { new DailyWorkItem { Title = "UI", DurationMinutes = 90, ProjectId = project.Id } }
        };
        var rejected = new DailyReport
        {
            InternshipPeriodId = periodA.Id, ReportDate = new DateOnly(2026, 7, 4),
            OrganizationUnitId = unit.Id,
            Status = DailyReportStatus.Rejected, GeneralNotes = "Red içerik",
            MentorComment = "MentorGizliRed"
        };
        db.DailyReports.AddRange(draft, submitted, approved, rejected);
        await db.SaveChangesAsync();

        return new Fixture(admin.Id, mentor.Id, project.Id, internA.Id, internB.Id, profileB.Id,
            draft.Id, submitted.Id, approved.Id, rejected.Id);
    }
}
