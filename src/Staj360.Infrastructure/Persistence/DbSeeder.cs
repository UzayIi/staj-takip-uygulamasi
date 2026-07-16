using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Identity;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// Rolleri, isteğe bağlı ilk yöneticiyi ve geliştirme verilerini seed eder.
/// Sabit production parolası KULLANMAZ; admin yalnızca SEED_ADMIN_* değerleri varsa oluşturulur.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, bool isDevelopment, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var config = services.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager);
        await SeedAdminAsync(userManager, config, logger);
        await SeedBaseDataAsync(db, cancellationToken);

        var seedSample = config.GetValue<bool>("Seed:SampleData");
        if (isDevelopment && seedSample)
            await SeedSampleDataAsync(db, userManager, logger, cancellationToken);

        // Gelişmiş demo seti: yalnızca Development + SEED_DEMO_DATA + SEED_DEMO_PASSWORD.
        await DemoDataSeeder.SeedAsync(services, isDevelopment, cancellationToken);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
        // Öncelik: environment variable, ardından configuration/user-secrets.
        var email = Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL") ?? config["SEED_ADMIN_EMAIL"];
        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? config["SEED_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SEED_ADMIN_EMAIL/PASSWORD tanımlı değil. Roller oluşturuldu, admin hesabı oluşturulmadı.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Sistem Yöneticisi",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, AppRoles.SuperAdmin);
            logger.LogInformation("İlk SuperAdmin hesabı oluşturuldu: {Email}", email);
        }
        else
        {
            logger.LogWarning("SuperAdmin oluşturulamadı: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task SeedBaseDataAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Departments.AnyAsync(cancellationToken))
        {
            db.Departments.AddRange(
                new Department { Name = "Yazılım Geliştirme", Description = "Yazılım ve uygulama geliştirme birimi", IsActive = true },
                new Department { Name = "Bilgi Teknolojileri", Description = "Altyapı ve sistem yönetimi", IsActive = true },
                new Department { Name = "İnsan Kaynakları", Description = "Personel ve staj süreçleri", IsActive = true });
        }

        if (!await db.WorkSchedules.AnyAsync(cancellationToken))
        {
            db.WorkSchedules.Add(new WorkSchedule
            {
                Name = "Standart Mesai (09:00-18:00, Hafta içi)",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(18, 0),
                GracePeriodMinutes = 15,
                MondayEnabled = true,
                TuesdayEnabled = true,
                WednesdayEnabled = true,
                ThursdayEnabled = true,
                FridayEnabled = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Geliştirme verisi: yalnızca Development + Seed:SampleData=true iken.
    private static async Task SeedSampleDataAsync(AppDbContext db, UserManager<ApplicationUser> userManager, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.InternProfiles.AnyAsync(cancellationToken))
            return;

        var department = await db.Departments.FirstAsync(cancellationToken);
        var schedule = await db.WorkSchedules.FirstAsync(cancellationToken);

        var mentor = await EnsureUserAsync(userManager, "mentor@staj360.local", "Örnek Danışman", AppRoles.Mentor, "Mentor!123");
        var internUser = await EnsureUserAsync(userManager, "stajyer@staj360.local", "Örnek Stajyer", AppRoles.Intern, "Intern!123");
        if (mentor is null || internUser is null) return;

        var profile = new InternProfile
        {
            UserId = internUser.Id,
            StudentNumber = "2024001",
            University = "Örnek Üniversitesi",
            DepartmentId = department.Id,
            IsActive = true
        };
        db.InternProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        var period = new InternshipPeriod
        {
            InternProfileId = profile.Id,
            MentorUserId = mentor.Id,
            WorkScheduleId = schedule.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            RequiredWorkDays = 40,
            Status = InternshipStatus.Active
        };
        db.InternshipPeriods.Add(period);
        await db.SaveChangesAsync(cancellationToken);

        // Örnek onaylı raporlar (AI özeti demosu için).
        for (var i = 1; i <= 3; i++)
        {
            var report = new DailyReport
            {
                InternshipPeriodId = period.Id,
                ReportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                GeneralNotes = $"{i}. gün genel çalışmalar tamamlandı.",
                Status = DailyReportStatus.Approved,
                SubmittedAtUtc = DateTime.UtcNow.AddDays(-i),
                ReviewedAtUtc = DateTime.UtcNow.AddDays(-i),
                ReviewedByUserId = mentor.Id,
                WorkItems = new List<DailyWorkItem>
                {
                    new() { Title = $"Görev {i}", Description = "Örnek geliştirme görevi", DurationMinutes = 240, TechnologiesUsed = "C#, ASP.NET Core" }
                }
            };
            db.DailyReports.Add(report);
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Geliştirme örnek verileri oluşturuldu.");
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string role, string password)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;

        var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName, IsActive = true, EmailConfirmed = true, CreatedAtUtc = DateTime.UtcNow };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) return null;
        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
