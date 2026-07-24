using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Internships;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Interns;

public class ManagerInternDetailScopeTests
{
    private sealed class FakeUsers : IUserDisplayLookup
    {
        private readonly Dictionary<Guid, UserDisplayInfo> _map = new();

        public void Add(Guid id, string name, string email) =>
            _map[id] = new UserDisplayInfo(id, name, email);

        public Task<IReadOnlyDictionary<Guid, UserDisplayInfo>> GetByIdsAsync(
            IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
        {
            var result = userIds
                .Where(id => _map.ContainsKey(id))
                .ToDictionary(id => id, id => _map[id]);
            return Task.FromResult((IReadOnlyDictionary<Guid, UserDisplayInfo>)result);
        }

        public Task<IReadOnlyList<Guid>> SearchUserIdsAsync(string search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(_map.Where(kv => kv.Value.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key).ToList());
    }

    [Fact]
    public async Task Manager_Can_Access_Intern_In_Assigned_Unit()
    {
        var utc = DateTime.UtcNow;
        var clock = new TestClock(utc);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var (managerId, otherManagerId, profileId, users) = await SeedAsync(db, utc);
            var service = new InternDetailService(db, users, new UnitAssignmentService(db, clock));

            var ok = await service.GetForViewerAsync(managerId, false, true, false, profileId);
            Assert.True(ok.Success);
            Assert.NotNull(ok.Data);
            Assert.Equal("Ayşe Yılmaz", ok.Data!.FullName);
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task Manager_Cannot_Access_Intern_Outside_Units()
    {
        var utc = DateTime.UtcNow;
        var clock = new TestClock(utc);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var (managerId, otherManagerId, profileId, users) = await SeedAsync(db, utc);
            var service = new InternDetailService(db, users, new UnitAssignmentService(db, clock));

            var denied = await service.GetForViewerAsync(otherManagerId, false, true, false, profileId);
            Assert.False(denied.Success);
            Assert.Equal("FORBIDDEN", denied.ErrorCode);
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task Admin_Can_Access_Any()
    {
        var utc = DateTime.UtcNow;
        var clock = new TestClock(utc);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var (_, otherManagerId, profileId, users) = await SeedAsync(db, utc);
            var service = new InternDetailService(db, users, new UnitAssignmentService(db, clock));

            var ok = await service.GetForViewerAsync(Guid.NewGuid(), true, false, false, profileId);
            Assert.True(ok.Success);
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }

    private static async Task<(Guid ManagerId, Guid OtherManagerId, Guid ProfileId, FakeUsers Users)> SeedAsync(
        Infrastructure.Persistence.AppDbContext db, DateTime utc)
    {
        var today = new FixedTimeZoneService().LocalDate(utc);
        var unitA = new OrganizationUnit
        {
            Code = "UA", Name = "Birim A", UnitType = OrganizationUnitType.Branch,
            IsActive = true, DisplayOrder = 1
        };
        var unitB = new OrganizationUnit
        {
            Code = "UB", Name = "Birim B", UnitType = OrganizationUnitType.Branch,
            IsActive = true, DisplayOrder = 2
        };
        var schedule = new WorkSchedule
        {
            Name = "Std",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            GracePeriodMinutes = 15,
            MondayEnabled = true, TuesdayEnabled = true, WednesdayEnabled = true,
            ThursdayEnabled = true, FridayEnabled = true
        };
        db.OrganizationUnits.AddRange(unitA, unitB);
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var internUserId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();

        var profile = new InternProfile
        {
            UserId = internUserId,
            StudentNumber = "STJ-2026-9999",
            University = "Dicle Üniversitesi",
            CurrentOrganizationUnitId = unitA.Id,
            IsActive = true
        };
        db.InternProfiles.Add(profile);
        await db.SaveChangesAsync();

        db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
        {
            ManagerUserId = managerId,
            OrganizationUnitId = unitA.Id,
            IsActive = true,
            AssignedAtUtc = utc
        });
        db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
        {
            ManagerUserId = otherManagerId,
            OrganizationUnitId = unitB.Id,
            IsActive = true,
            AssignedAtUtc = utc
        });
        db.InternUnitAssignments.Add(new InternUnitAssignment
        {
            InternProfileId = profile.Id,
            OrganizationUnitId = unitA.Id,
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

        var users = new FakeUsers();
        users.Add(internUserId, "Ayşe Yılmaz", "stajyer@gmail.com");
        users.Add(mentorId, "Aylin Demirtaş", "danisman@gmail.com");
        return (managerId, otherManagerId, profile.Id, users);
    }
}
