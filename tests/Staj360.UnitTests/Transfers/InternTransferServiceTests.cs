using Microsoft.EntityFrameworkCore;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Transfers;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Transfers;

public class InternTransferServiceTests
{
    private static async Task<(
        Staj360.Infrastructure.Persistence.AppDbContext Db,
        Guid ManagerId,
        Guid OtherManagerId,
        Guid MentorId,
        Guid ProfileId,
        Guid SourceUnitId,
        Guid TargetUnitId)> SeedTransferGraphAsync(DateTime utc)
    {
        var clock = new TestClock(utc);
        var db = TestDbFactory.Create(clock);
        var today = new FixedTimeZoneService().LocalDate(utc);

        var source = new OrganizationUnit { Code = "SRC", Name = "Kaynak", UnitType = OrganizationUnitType.Branch, IsActive = true, DisplayOrder = 1 };
        var target = new OrganizationUnit { Code = "TGT", Name = "Hedef", UnitType = OrganizationUnitType.Branch, IsActive = true, DisplayOrder = 2 };
        var schedule = new WorkSchedule
        {
            Name = "Std",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            GracePeriodMinutes = 15,
            MondayEnabled = true, TuesdayEnabled = true, WednesdayEnabled = true,
            ThursdayEnabled = true, FridayEnabled = true
        };
        db.OrganizationUnits.AddRange(source, target);
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var internUserId = Guid.NewGuid();

        var profile = new InternProfile
        {
            UserId = internUserId,
            StudentNumber = "TR-1001",
            CurrentOrganizationUnitId = source.Id,
            IsActive = true
        };
        db.InternProfiles.Add(profile);
        await db.SaveChangesAsync();

        db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
        {
            ManagerUserId = managerId,
            OrganizationUnitId = source.Id,
            IsActive = true,
            AssignedAtUtc = utc
        });
        db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
        {
            ManagerUserId = otherManagerId,
            OrganizationUnitId = target.Id,
            IsActive = true,
            AssignedAtUtc = utc
        });
        db.AdvisorUnitAssignments.Add(new AdvisorUnitAssignment
        {
            AdvisorUserId = mentorId,
            OrganizationUnitId = target.Id,
            IsActive = true,
            AssignedAtUtc = utc
        });
        db.InternUnitAssignments.Add(new InternUnitAssignment
        {
            InternProfileId = profile.Id,
            OrganizationUnitId = source.Id,
            AdvisorUserId = mentorId,
            StartDate = today.AddDays(-10),
            IsActive = true
        });
        db.InternshipPeriods.Add(new InternshipPeriod
        {
            InternProfileId = profile.Id,
            MentorUserId = mentorId,
            WorkScheduleId = schedule.Id,
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(40),
            RequiredWorkDays = 40,
            Status = InternshipStatus.Active
        });
        await db.SaveChangesAsync();

        return (db, managerId, otherManagerId, mentorId, profile.Id, source.Id, target.Id);
    }

    private static InternTransferService CreateService(Staj360.Infrastructure.Persistence.AppDbContext db, DateTime utc) =>
        new(db, new TestClock(utc), new FixedTimeZoneService(), new UnitAssignmentService(db, new TestClock(utc)), new NoOpAuditLogService());

    [Fact]
    public async Task SourceManager_CanCreatePendingTransfer()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, managerId, _, _, profileId, _, targetId) = await SeedTransferGraphAsync(utc);
        try
        {
            var svc = CreateService(db, utc);
            var result = await svc.CreateAsync(managerId, new CreateTransferCommand(profileId, targetId, "sebep"));
            Assert.True(result.Success);
            Assert.Equal(TransferRequestStatus.Pending, result.Data!.Status);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task UnauthorizedManager_CannotCreate()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, _, otherManagerId, _, profileId, _, targetId) = await SeedTransferGraphAsync(utc);
        try
        {
            var svc = CreateService(db, utc);
            var result = await svc.CreateAsync(otherManagerId, new CreateTransferCommand(profileId, targetId, null));
            Assert.False(result.Success);
            Assert.Equal("FORBIDDEN", result.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task TargetManager_CanApprove_AndOldReportUnitUnchanged()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, managerId, otherManagerId, mentorId, profileId, sourceId, targetId) = await SeedTransferGraphAsync(utc);
        try
        {
            var reportService = new Staj360.Application.Services.DailyReports.DailyReportService(
                db, new TestClock(utc), new FixedTimeZoneService(), new NoOpAuditLogService());
            var internUserId = (await db.InternProfiles.FirstAsync(p => p.Id == profileId)).UserId;
            var report = await reportService.CreateAsync(internUserId, new Staj360.Application.Services.DailyReports.CreateDailyReportCommand("eski", null, null, null));
            Assert.True(report.Success);
            var oldUnit = report.Data!.OrganizationUnitId;
            Assert.Equal(sourceId, oldUnit);

            var svc = CreateService(db, utc);
            var created = await svc.CreateAsync(managerId, new CreateTransferCommand(profileId, targetId, "sebep"));
            Assert.True(created.Success);

            var decide = await svc.DecideAsync(otherManagerId, new DecideTransferCommand(created.Data!.Id, true, mentorId, "ok", null));
            Assert.True(decide.Success);

            var refreshed = await db.DailyReports.AsNoTracking().FirstAsync(r => r.Id == report.Data.Id);
            Assert.Equal(oldUnit, refreshed.OrganizationUnitId);

            var profile = await db.InternProfiles.AsNoTracking().FirstAsync(p => p.Id == profileId);
            Assert.Equal(targetId, profile.CurrentOrganizationUnitId);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task SameManagerBothUnits_CanDirectComplete()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, managerId, _, mentorId, profileId, _, targetId) = await SeedTransferGraphAsync(utc);
        try
        {
            db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
            {
                ManagerUserId = managerId,
                OrganizationUnitId = targetId,
                IsActive = true,
                AssignedAtUtc = utc
            });
            await db.SaveChangesAsync();

            var svc = CreateService(db, utc);
            var result = await svc.CreateAsync(managerId, new CreateTransferCommand(
                profileId, targetId, "doğrudan", null, mentorId, ExecuteImmediatelyIfSameManager: true));
            Assert.True(result.Success);
            Assert.Equal(TransferRequestStatus.Approved, result.Data!.Status);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task AlreadyDecided_CannotDecideAgain()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, managerId, otherManagerId, mentorId, profileId, _, targetId) = await SeedTransferGraphAsync(utc);
        try
        {
            var svc = CreateService(db, utc);
            var created = await svc.CreateAsync(managerId, new CreateTransferCommand(profileId, targetId, null));
            Assert.True((await svc.DecideAsync(otherManagerId, new DecideTransferCommand(created.Data!.Id, false, null, "red", null))).Success);
            var second = await svc.DecideAsync(otherManagerId, new DecideTransferCommand(created.Data.Id, true, mentorId, "tekrar", null));
            Assert.False(second.Success);
            Assert.Equal("ALREADY_DECIDED", second.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }
}
