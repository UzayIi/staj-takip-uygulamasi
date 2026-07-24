using Microsoft.EntityFrameworkCore;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Organization;
using Staj360.Application.Services.Transfers;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Transfers;

public class TransferCascadeValidationTests
{
    [Fact]
    public async Task ValidateBranch_Rejects_Wrong_Parent()
    {
        var clock = new TestClock(DateTime.UtcNow);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var dirA = new OrganizationUnit
            {
                Code = "DIR_A", Name = "Daire A", UnitType = OrganizationUnitType.Directorate,
                IsActive = true, DisplayOrder = 1
            };
            var dirB = new OrganizationUnit
            {
                Code = "DIR_B", Name = "Daire B", UnitType = OrganizationUnitType.Directorate,
                IsActive = true, DisplayOrder = 2
            };
            db.OrganizationUnits.AddRange(dirA, dirB);
            await db.SaveChangesAsync();

            var branch = new OrganizationUnit
            {
                Code = "BR_A", Name = "Şube A", UnitType = OrganizationUnitType.Branch,
                ParentId = dirA.Id, IsActive = true, DisplayOrder = 1
            };
            db.OrganizationUnits.Add(branch);
            await db.SaveChangesAsync();

            var units = new OrganizationUnitService(db);
            var ok = await units.ValidateBranchBelongsToDirectorateAsync(branch.Id, dirA.Id);
            Assert.True(ok.Success);

            var bad = await units.ValidateBranchBelongsToDirectorateAsync(branch.Id, dirB.Id);
            Assert.False(bad.Success);
            Assert.Equal("INVALID_PARENT", bad.ErrorCode);
            Assert.Contains("bağlı değildir", bad.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task CreateTransfer_Rejects_Directorate_As_Target()
    {
        var utc = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(utc);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var today = new FixedTimeZoneService().LocalDate(utc);
            var dir = new OrganizationUnit
            {
                Code = "DIR", Name = "Daire", UnitType = OrganizationUnitType.Directorate,
                IsActive = true, DisplayOrder = 1
            };
            db.OrganizationUnits.Add(dir);
            await db.SaveChangesAsync();

            var source = new OrganizationUnit
            {
                Code = "SRC", Name = "Kaynak", UnitType = OrganizationUnitType.Branch,
                ParentId = dir.Id, IsActive = true, DisplayOrder = 1
            };
            db.OrganizationUnits.Add(source);
            var schedule = new WorkSchedule
            {
                Name = "Std",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                GracePeriodMinutes = 15,
                MondayEnabled = true, TuesdayEnabled = true, WednesdayEnabled = true,
                ThursdayEnabled = true, FridayEnabled = true
            };
            db.WorkSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var managerId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var profile = new InternProfile
            {
                UserId = Guid.NewGuid(),
                StudentNumber = "TR-CASCADE-1",
                CurrentOrganizationUnitId = source.Id,
                IsActive = true
            };
            db.InternProfiles.Add(profile);
            db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
            {
                ManagerUserId = managerId,
                OrganizationUnitId = source.Id,
                IsActive = true,
                AssignedAtUtc = utc
            });
            db.InternUnitAssignments.Add(new InternUnitAssignment
            {
                InternProfileId = profile.Id,
                OrganizationUnitId = source.Id,
                AdvisorUserId = mentorId,
                StartDate = today.AddDays(-5),
                IsActive = true
            });
            db.InternshipPeriods.Add(new InternshipPeriod
            {
                InternProfileId = profile.Id,
                MentorUserId = mentorId,
                WorkScheduleId = schedule.Id,
                StartDate = today.AddDays(-5),
                EndDate = today.AddDays(40),
                RequiredWorkDays = 40,
                Status = InternshipStatus.Active
            });
            await db.SaveChangesAsync();

            var service = new InternTransferService(
                db, clock, new FixedTimeZoneService(),
                new UnitAssignmentService(db, clock), new NoOpAuditLogService());

            var result = await service.CreateAsync(managerId, new CreateTransferCommand(
                profile.Id, dir.Id, "deneme", today.AddDays(1), mentorId, false));

            Assert.False(result.Success);
            Assert.Equal("INVALID_TARGET", result.ErrorCode);
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task ListBranchesByDirectorate_Returns_Only_Children()
    {
        var clock = new TestClock(DateTime.UtcNow);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var dir = new OrganizationUnit
            {
                Code = "BI", Name = "Bilgi İşlem", UnitType = OrganizationUnitType.Directorate,
                IsActive = true, DisplayOrder = 1
            };
            var other = new OrganizationUnit
            {
                Code = "OT", Name = "Diğer", UnitType = OrganizationUnitType.Directorate,
                IsActive = true, DisplayOrder = 2
            };
            db.OrganizationUnits.AddRange(dir, other);
            await db.SaveChangesAsync();

            db.OrganizationUnits.AddRange(
                new OrganizationUnit
                {
                    Code = "CBS", Name = "CBS", UnitType = OrganizationUnitType.Branch,
                    ParentId = dir.Id, IsActive = true, DisplayOrder = 1
                },
                new OrganizationUnit
                {
                    Code = "XX", Name = "Yabancı", UnitType = OrganizationUnitType.Branch,
                    ParentId = other.Id, IsActive = true, DisplayOrder = 1
                });
            await db.SaveChangesAsync();

            var units = new OrganizationUnitService(db);
            var branches = await units.ListBranchesByDirectorateAsync(dir.Id);
            Assert.Single(branches);
            Assert.Equal("CBS", branches[0].Code);
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }
}
