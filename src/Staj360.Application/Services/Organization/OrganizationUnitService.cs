using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Organization;

public record OrganizationUnitDto(
    Guid Id, string Code, string Name, OrganizationUnitType UnitType,
    Guid? ParentId, string? ParentName, int DisplayOrder, bool IsActive);

public interface IOrganizationUnitService
{
    Task<IReadOnlyList<OrganizationUnitDto>> ListTreeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationUnitDto>> ListBranchesAsync(CancellationToken cancellationToken = default);
    Task<OrganizationUnitDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid?> GetIdByCodeAsync(string code, CancellationToken cancellationToken = default);
}

public class OrganizationUnitService : IOrganizationUnitService
{
    private readonly IApplicationDbContext _db;

    public OrganizationUnitService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<OrganizationUnitDto>> ListTreeAsync(CancellationToken cancellationToken = default)
    {
        var units = await _db.OrganizationUnits.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.DisplayOrder).ThenBy(u => u.Name)
            .Select(u => new { u.Id, u.Code, u.Name, u.UnitType, u.ParentId, ParentName = u.Parent != null ? u.Parent.Name : null, u.DisplayOrder, u.IsActive })
            .ToListAsync(cancellationToken);

        return units.Select(u => new OrganizationUnitDto(u.Id, u.Code, u.Name, u.UnitType, u.ParentId, u.ParentName, u.DisplayOrder, u.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<OrganizationUnitDto>> ListBranchesAsync(CancellationToken cancellationToken = default) =>
        await _db.OrganizationUnits.AsNoTracking()
            .Where(u => !u.IsDeleted && u.UnitType == OrganizationUnitType.Branch && u.IsActive)
            .OrderBy(u => u.Parent!.DisplayOrder).ThenBy(u => u.DisplayOrder).ThenBy(u => u.Name)
            .Select(u => new OrganizationUnitDto(u.Id, u.Code, u.Name, u.UnitType, u.ParentId, u.Parent != null ? u.Parent.Name : null, u.DisplayOrder, u.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<OrganizationUnitDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await _db.OrganizationUnits.AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.Name, x.UnitType, x.ParentId, ParentName = x.Parent != null ? x.Parent.Name : null, x.DisplayOrder, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        return u is null ? null : new OrganizationUnitDto(u.Id, u.Code, u.Name, u.UnitType, u.ParentId, u.ParentName, u.DisplayOrder, u.IsActive);
    }

    public async Task<Guid?> GetIdByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await _db.OrganizationUnits.AsNoTracking()
            .Where(u => u.Code == code && !u.IsDeleted)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
