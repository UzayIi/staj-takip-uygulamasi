using Staj360.Application.Services.Attendance;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Persistence;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Attendance;

public class AttendanceServiceTests
{
    private static AttendanceActionContext WebContext() =>
        new(AttendanceSource.WebButton, "127.0.0.1", "xunit");

    [Fact]
    public async Task CheckIn_FirstTime_Succeeds()
    {
        var utc = new DateTime(2026, 7, 13, 6, 0, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = new AttendanceService(db, new TestClock(utc), new FixedTimeZoneService());
            var result = await service.CheckInAsync(userId, WebContext());

            Assert.True(result.Success);
            Assert.True(result.Data!.HasOpenCheckIn);
            Assert.Equal(AttendanceStatus.Present, result.Data.Status);
            Assert.False(result.Data.IsLate);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task CheckIn_WhenOpenCheckInExists_IsRejected()
    {
        var utc = new DateTime(2026, 7, 13, 6, 0, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = new AttendanceService(db, new TestClock(utc), new FixedTimeZoneService());
            Assert.True((await service.CheckInAsync(userId, WebContext())).Success);
            var second = await service.CheckInAsync(userId, WebContext());

            Assert.False(second.Success);
            Assert.Equal("ALREADY_CHECKED_IN", second.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task CheckOut_WithoutCheckIn_IsRejected()
    {
        var utc = new DateTime(2026, 7, 13, 6, 0, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = new AttendanceService(db, new TestClock(utc), new FixedTimeZoneService());
            var result = await service.CheckOutAsync(userId, WebContext());

            Assert.False(result.Success);
            Assert.Equal("NOT_CHECKED_IN", result.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task CheckIn_AfterGracePeriod_MarksLate()
    {
        var utc = new DateTime(2026, 7, 13, 6, 20, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0), graceMinutes: 15);
        try
        {
            var service = new AttendanceService(db, new TestClock(utc), new FixedTimeZoneService());
            var result = await service.CheckInAsync(userId, WebContext());

            Assert.True(result.Success);
            Assert.True(result.Data!.IsLate);
            Assert.Equal(AttendanceStatus.Late, result.Data.Status);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task MultipleCheckInOut_Pairs_SumCorrectly()
    {
        var start = new DateTime(2026, 7, 13, 6, 0, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(start, new TimeOnly(9, 0));
        try
        {
            var clock = new TestClock(start);
            var service = new AttendanceService(db, clock, new FixedTimeZoneService());

            Assert.True((await service.CheckInAsync(userId, WebContext())).Success);

            clock.UtcNow = start.AddHours(2);
            Assert.True((await service.CheckOutAsync(userId, WebContext())).Success, "first checkout");

            clock.UtcNow = start.AddHours(3);
            Assert.True((await service.CheckInAsync(userId, WebContext())).Success);

            clock.UtcNow = start.AddHours(4);
            var final = await service.CheckOutAsync(userId, WebContext());

            Assert.True(final.Success, final.ErrorMessage);
            Assert.Equal(180, final.Data!.TotalWorkedMinutes);
            Assert.False(final.Data.HasOpenCheckIn);
            Assert.False(final.Data.IsIncomplete);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task CheckOut_OnNewDbContext_AfterCheckIn_Succeeds()
    {
        // HTTP istekleri gibi ayrı DbContext (scope) simülasyonu.
        var start = new DateTime(2026, 7, 13, 6, 0, 0, DateTimeKind.Utc);
        var dbName = "Staj360Ctx_" + Guid.NewGuid().ToString("N");
        var clock = new TestClock(start);
        var db1 = TestDbFactory.Create(clock, dbName);
        AppDbContext? db2 = null;
        try
        {
            var tz = new FixedTimeZoneService();
            var today = tz.LocalDate(start);
            var userId = Guid.NewGuid();
            var unit = new Staj360.Domain.Entities.OrganizationUnit
            {
                Code = "YAZILIM",
                Name = "Yazılım",
                UnitType = OrganizationUnitType.Branch,
                DisplayOrder = 1,
                IsActive = true
            };
            var schedule = new Staj360.Domain.Entities.WorkSchedule
            {
                Name = "S", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0), GracePeriodMinutes = 15,
                MondayEnabled = true, TuesdayEnabled = true, WednesdayEnabled = true, ThursdayEnabled = true,
                FridayEnabled = true, SaturdayEnabled = true, SundayEnabled = true
            };
            var profile = new Staj360.Domain.Entities.InternProfile
            {
                UserId = userId, StudentNumber = "9", CurrentOrganizationUnit = unit, IsActive = true
            };
            var period = new Staj360.Domain.Entities.InternshipPeriod
            {
                InternProfile = profile, MentorUserId = Guid.NewGuid(), WorkSchedule = schedule,
                StartDate = today.AddDays(-1), EndDate = today.AddDays(30), RequiredWorkDays = 10,
                Status = InternshipStatus.Active
            };
            db1.AddRange(unit, schedule, profile, period);
            await db1.SaveChangesAsync();

            Assert.True((await new AttendanceService(db1, clock, tz).CheckInAsync(userId, WebContext())).Success);
            await db1.DisposeAsync();
            db1 = null!;

            clock.UtcNow = start.AddHours(2);
            db2 = TestDbFactory.Create(clock, dbName);
            var result = await new AttendanceService(db2, clock, tz).CheckOutAsync(userId, WebContext());
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(120, result.Data!.TotalWorkedMinutes);
        }
        finally
        {
            if (db2 is not null) await TestDbFactory.DisposeDatabaseAsync(db2);
            else if (db1 is not null) await TestDbFactory.DisposeDatabaseAsync(db1);
        }
    }

    [Fact]
    public async Task CheckIn_WithoutActivePeriod_IsRejected()
    {
        var utc = new DateTime(2026, 7, 13, 6, 0, 0, DateTimeKind.Utc);
        var db = TestDbFactory.Create(new TestClock(utc));
        try
        {
            var service = new AttendanceService(db, new TestClock(utc), new FixedTimeZoneService());
            var result = await service.CheckInAsync(Guid.NewGuid(), WebContext());

            Assert.False(result.Success);
            Assert.Equal("NO_ACTIVE_PERIOD", result.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }
}
