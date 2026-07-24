using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Persistence;

namespace Staj360.UnitTests.TestSupport;

public static class TestDbFactory
{
    private const string SqlServer =
        "Server=localhost\\SQLEXPRESS01;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public static AppDbContext Create(IClock? clock = null, string? databaseName = null)
    {
        // Gerçek SQL Express: RowVersion/concurrency üretim ortamıyla uyumlu.
        var dbName = databaseName ?? ("Staj360Test_" + Guid.NewGuid().ToString("N"));
        var connectionString = SqlServer + $";Database={dbName}";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .EnableSensitiveDataLogging()
            .Options;

        var db = new AppDbContext(options, clock);
        db.Database.EnsureCreated();
        return db;
    }

    public static async Task<(AppDbContext Db, Guid UserId, Guid MentorId, Guid PeriodId, Guid ProfileId)> SeedActiveInternAsync(
        DateTime utcNow,
        TimeOnly scheduleStart,
        int graceMinutes = 15,
        bool seedUnitAssignment = true)
    {
        var clock = new TestClock(utcNow);
        var db = Create(clock);
        var tz = new FixedTimeZoneService();
        var today = tz.LocalDate(utcNow);

        var userId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var organizationUnit = new OrganizationUnit
        {
            Code = "YAZILIM",
            Name = "Yazılım",
            UnitType = OrganizationUnitType.Branch,
            DisplayOrder = 1,
            IsActive = true
        };
        var schedule = new WorkSchedule
        {
            Name = "Hafta içi",
            StartTime = scheduleStart,
            EndTime = new TimeOnly(17, 0),
            GracePeriodMinutes = graceMinutes,
            MondayEnabled = true,
            TuesdayEnabled = true,
            WednesdayEnabled = true,
            ThursdayEnabled = true,
            FridayEnabled = true,
            SaturdayEnabled = true,
            SundayEnabled = true
        };
        var profile = new InternProfile
        {
            UserId = userId,
            StudentNumber = "2026001",
            CurrentOrganizationUnit = organizationUnit,
            IsActive = true
        };
        var period = new InternshipPeriod
        {
            InternProfile = profile,
            MentorUserId = mentorId,
            WorkSchedule = schedule,
            StartDate = today.AddDays(-30),
            EndDate = today.AddDays(60),
            RequiredWorkDays = 40,
            Status = InternshipStatus.Active
        };

        db.OrganizationUnits.Add(organizationUnit);
        db.WorkSchedules.Add(schedule);
        db.InternProfiles.Add(profile);
        db.InternshipPeriods.Add(period);
        await db.SaveChangesAsync();

        if (seedUnitAssignment)
        {
            db.InternUnitAssignments.Add(new InternUnitAssignment
            {
                InternProfileId = profile.Id,
                OrganizationUnitId = organizationUnit.Id,
                AdvisorUserId = mentorId,
                StartDate = today.AddDays(-30),
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        return (db, userId, mentorId, period.Id, profile.Id);
    }

    public static async Task DisposeDatabaseAsync(AppDbContext db)
    {
        await db.Database.EnsureDeletedAsync();
        await db.DisposeAsync();
    }
}
