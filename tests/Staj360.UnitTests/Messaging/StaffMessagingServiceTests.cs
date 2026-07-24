using Staj360.Application.Abstractions;
using Staj360.Application.Services.Messaging;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Messaging;

public class StaffMessagingServiceTests
{
    private sealed class FakeUsers : IUserDisplayLookup
    {
        public Task<IReadOnlyDictionary<Guid, UserDisplayInfo>> GetByIdsAsync(
            IEnumerable<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            var map = userIds.Distinct().ToDictionary(
                id => id,
                id => new UserDisplayInfo(id, "Kullanıcı " + id.ToString("N")[..6], id.ToString("N")[..6] + "@test.local"));
            return Task.FromResult<IReadOnlyDictionary<Guid, UserDisplayInfo>>(map);
        }

        public Task<IReadOnlyList<Guid>> SearchUserIdsAsync(string search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
    }

    [Fact]
    public async Task SharedUnit_ManagerAndMentor_CanMessage()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(utc);
        var db = TestDbFactory.Create(clock);
        try
        {
            var unit = new OrganizationUnit { Code = "MSG", Name = "Mesaj", UnitType = OrganizationUnitType.Branch, IsActive = true, DisplayOrder = 1 };
            db.OrganizationUnits.Add(unit);
            var managerId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var strangerId = Guid.NewGuid();
            db.ManagerUnitAssignments.Add(new ManagerUnitAssignment { ManagerUserId = managerId, OrganizationUnitId = unit.Id, IsActive = true, AssignedAtUtc = utc });
            db.AdvisorUnitAssignments.Add(new AdvisorUnitAssignment { AdvisorUserId = mentorId, OrganizationUnitId = unit.Id, IsActive = true, AssignedAtUtc = utc });
            await db.SaveChangesAsync();

            var svc = new StaffMessagingService(db, clock, new NoOpAuditLogService(), new FakeUsers());
            var ok = await svc.SendAsync(managerId, new SendStaffMessageCommand(mentorId, unit.Id, "Konu", "Merhaba danışman"));
            Assert.True(ok.Success);

            var forbidden = await svc.SendAsync(managerId, new SendStaffMessageCommand(strangerId, unit.Id, "Konu", "Merhaba"));
            Assert.False(forbidden.Success);
            Assert.Equal("FORBIDDEN", forbidden.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task Idor_CannotReadOthersMessage()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(utc);
        var db = TestDbFactory.Create(clock);
        try
        {
            var unit = new OrganizationUnit { Code = "MSG2", Name = "Mesaj2", UnitType = OrganizationUnitType.Branch, IsActive = true, DisplayOrder = 1 };
            db.OrganizationUnits.Add(unit);
            var managerId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            db.ManagerUnitAssignments.Add(new ManagerUnitAssignment { ManagerUserId = managerId, OrganizationUnitId = unit.Id, IsActive = true, AssignedAtUtc = utc });
            db.AdvisorUnitAssignments.Add(new AdvisorUnitAssignment { AdvisorUserId = mentorId, OrganizationUnitId = unit.Id, IsActive = true, AssignedAtUtc = utc });
            await db.SaveChangesAsync();

            var svc = new StaffMessagingService(db, clock, new NoOpAuditLogService(), new FakeUsers());
            var sent = await svc.SendAsync(managerId, new SendStaffMessageCommand(mentorId, unit.Id, "K", "B"));
            Assert.True(sent.Success);

            var other = await svc.GetDetailsAsync(Guid.NewGuid(), sent.Data!.Id);
            Assert.False(other.Success);
            Assert.True(other.ErrorCode is "FORBIDDEN" or "NOT_FOUND");
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }
}
