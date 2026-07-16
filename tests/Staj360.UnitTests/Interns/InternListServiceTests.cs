using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Staj360.Application.Common;
using Staj360.Application.Services.Internships;
using Staj360.Domain.Entities;
using Staj360.Infrastructure.Configuration;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.Infrastructure.Services;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Interns;

public class InternListServiceTests
{
    [Fact]
    public async Task ListAsync_Returns_FullName_From_ApplicationUser()
    {
        var utc = new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc);
        var (sp, dbName) = await CreateHostAsync(utc);
        await using (sp)
        {
            try
            {
                var db = sp.GetRequiredService<AppDbContext>();
                var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var service = sp.GetRequiredService<IInternService>();

                var user = new ApplicationUser
                {
                    UserName = "ayse.list@test.local",
                    Email = "ayse.list@test.local",
                    FullName = "Ayşe Yılmaz",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAtUtc = utc
                };
                Assert.True((await users.CreateAsync(user, "Test!12345")).Succeeded);
                await users.AddToRoleAsync(user, AppRoles.Intern);

                var dept = new Department { Name = "Yazılım Geliştirme", IsActive = true };
                db.Departments.Add(dept);
                await db.SaveChangesAsync();
                db.InternProfiles.Add(new InternProfile
                {
                    UserId = user.Id,
                    StudentNumber = "LIST-1",
                    University = "Fırat Üniversitesi",
                    SchoolDepartment = "Yazılım Mühendisliği",
                    DepartmentId = dept.Id,
                    IsActive = true
                });
                await db.SaveChangesAsync();

                var result = await service.ListAsync(null, 1, 20);
                var row = Assert.Single(result.Items);
                Assert.Equal("Ayşe Yılmaz", row.FullName);
                Assert.Equal("ayse.list@test.local", row.Email);
                Assert.False(string.IsNullOrWhiteSpace(row.FullName));
            }
            finally
            {
                await DropDbAsync(sp);
            }
        }
    }

    [Fact]
    public async Task ListAsync_Search_By_FullName_Works()
    {
        var utc = new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc);
        var (sp, _) = await CreateHostAsync(utc);
        await using (sp)
        {
            try
            {
                var db = sp.GetRequiredService<AppDbContext>();
                var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var service = sp.GetRequiredService<IInternService>();
                var dept = new Department { Name = "Bilgi İşlem", IsActive = true };
                db.Departments.Add(dept);
                await db.SaveChangesAsync();

                await AddInternAsync(users, db, dept.Id, "mehmet.list@test.local", "Mehmet Kaya", "LIST-2", utc);
                await AddInternAsync(users, db, dept.Id, "zeynep.list@test.local", "Zeynep Demir", "LIST-3", utc);

                var result = await service.ListAsync("Zeynep", 1, 20);
                Assert.Single(result.Items);
                Assert.Equal("Zeynep Demir", result.Items[0].FullName);
                Assert.All(result.Items, i => Assert.Contains("Zeynep", i.FullName, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                await DropDbAsync(sp);
            }
        }
    }

    private static async Task AddInternAsync(
        UserManager<ApplicationUser> users, AppDbContext db, Guid deptId,
        string email, string fullName, string studentNo, DateTime utc)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName,
            IsActive = true, EmailConfirmed = true, CreatedAtUtc = utc
        };
        Assert.True((await users.CreateAsync(user, "Test!12345")).Succeeded);
        await users.AddToRoleAsync(user, AppRoles.Intern);
        db.InternProfiles.Add(new InternProfile
        {
            UserId = user.Id, StudentNumber = studentNo, DepartmentId = deptId, IsActive = true, University = "Test Üni"
        });
        await db.SaveChangesAsync();
    }

    private static async Task<(ServiceProvider Sp, string DbName)> CreateHostAsync(DateTime utc)
    {
        var dbName = "Staj360InternList_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Staj360.Application.Abstractions.IClock>(new TestClock(utc));
        services.AddSingleton<Staj360.Application.Abstractions.ITimeZoneService>(new FixedTimeZoneService());
        services.AddSingleton(Options.Create(new OrganizationOptions { Name = "Test", TimeZone = "Europe/Istanbul" }));
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer($"Server=localhost\\SQLEXPRESS01;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IInternService, InternService>();

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roles.RoleExistsAsync(AppRoles.Intern))
            await roles.CreateAsync(new ApplicationRole(AppRoles.Intern));
        return (sp, dbName);
    }

    private static async Task DropDbAsync(ServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
    }
}
