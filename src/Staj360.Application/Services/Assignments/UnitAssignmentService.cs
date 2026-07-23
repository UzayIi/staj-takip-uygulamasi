using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Assignments;

public interface IUnitAssignmentService
{
    Task<IReadOnlyList<Guid>> GetManagerUnitIdsAsync(Guid managerUserId, CancellationToken cancellationToken = default);
    Task<bool> IsManagerOfUnitAsync(Guid managerUserId, Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<ServiceResult> AssignManagerAsync(Guid managerUserId, Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<ServiceResult> UnassignManagerAsync(Guid managerUserId, Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<ServiceResult> AssignAdvisorAsync(Guid advisorUserId, Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<ServiceResult> UnassignAdvisorAsync(Guid advisorUserId, Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetAdvisorUnitIdsAsync(Guid advisorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetActiveManagerUserIdsForUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<InternUnitAssignment?> GetActiveInternAssignmentAsync(Guid internProfileId, CancellationToken cancellationToken = default);
}

public class UnitAssignmentService : IUnitAssignmentService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public UnitAssignmentService(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Guid>> GetManagerUnitIdsAsync(Guid managerUserId, CancellationToken cancellationToken = default) =>
        await _db.ManagerUnitAssignments.AsNoTracking()
            .Where(a => a.ManagerUserId == managerUserId && a.IsActive && !a.IsDeleted)
            .Select(a => a.OrganizationUnitId)
            .ToListAsync(cancellationToken);

    public async Task<bool> IsManagerOfUnitAsync(Guid managerUserId, Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        await _db.ManagerUnitAssignments.AsNoTracking()
            .AnyAsync(a => a.ManagerUserId == managerUserId && a.OrganizationUnitId == organizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetAdvisorUnitIdsAsync(Guid advisorUserId, CancellationToken cancellationToken = default) =>
        await _db.AdvisorUnitAssignments.AsNoTracking()
            .Where(a => a.AdvisorUserId == advisorUserId && a.IsActive && !a.IsDeleted)
            .Select(a => a.OrganizationUnitId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetActiveManagerUserIdsForUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        await _db.ManagerUnitAssignments.AsNoTracking()
            .Where(a => a.OrganizationUnitId == organizationUnitId && a.IsActive && !a.IsDeleted)
            .Select(a => a.ManagerUserId)
            .ToListAsync(cancellationToken);

    public async Task<InternUnitAssignment?> GetActiveInternAssignmentAsync(Guid internProfileId, CancellationToken cancellationToken = default) =>
        await _db.InternUnitAssignments
            .FirstOrDefaultAsync(a => a.InternProfileId == internProfileId && a.IsActive && !a.IsDeleted, cancellationToken);

    public async Task<ServiceResult> AssignManagerAsync(Guid managerUserId, Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var unit = await _db.OrganizationUnits.FirstOrDefaultAsync(u => u.Id == organizationUnitId && !u.IsDeleted, cancellationToken);
        if (unit is null || unit.UnitType != OrganizationUnitType.Branch)
            return ServiceResult.Fail("Yalnızca şube müdürlüklerine yönetici atanabilir.", "INVALID_UNIT");

        var exists = await _db.ManagerUnitAssignments.AnyAsync(
            a => a.ManagerUserId == managerUserId && a.OrganizationUnitId == organizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);
        if (exists)
            return ServiceResult.Fail("Bu yönetici zaten bu birime atanmış.", "ALREADY_ASSIGNED");

        _db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
        {
            ManagerUserId = managerUserId,
            OrganizationUnitId = organizationUnitId,
            IsActive = true,
            AssignedAtUtc = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnassignManagerAsync(Guid managerUserId, Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var assignment = await _db.ManagerUnitAssignments.FirstOrDefaultAsync(
            a => a.ManagerUserId == managerUserId && a.OrganizationUnitId == organizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);
        if (assignment is null)
            return ServiceResult.Fail("Atama bulunamadı.", "NOT_FOUND");

        assignment.IsActive = false;
        assignment.EndedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AssignAdvisorAsync(Guid advisorUserId, Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var unit = await _db.OrganizationUnits.FirstOrDefaultAsync(u => u.Id == organizationUnitId && !u.IsDeleted, cancellationToken);
        if (unit is null || unit.UnitType != OrganizationUnitType.Branch)
            return ServiceResult.Fail("Yalnızca şube müdürlüklerine danışman atanabilir.", "INVALID_UNIT");

        var exists = await _db.AdvisorUnitAssignments.AnyAsync(
            a => a.AdvisorUserId == advisorUserId && a.OrganizationUnitId == organizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);
        if (exists)
            return ServiceResult.Fail("Bu danışman zaten bu birime atanmış.", "ALREADY_ASSIGNED");

        _db.AdvisorUnitAssignments.Add(new AdvisorUnitAssignment
        {
            AdvisorUserId = advisorUserId,
            OrganizationUnitId = organizationUnitId,
            IsActive = true,
            AssignedAtUtc = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnassignAdvisorAsync(Guid advisorUserId, Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var assignment = await _db.AdvisorUnitAssignments.FirstOrDefaultAsync(
            a => a.AdvisorUserId == advisorUserId && a.OrganizationUnitId == organizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);
        if (assignment is null)
            return ServiceResult.Fail("Atama bulunamadı.", "NOT_FOUND");

        assignment.IsActive = false;
        assignment.EndedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}
