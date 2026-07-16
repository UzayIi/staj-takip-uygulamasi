using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Departments;

public class DepartmentService : IDepartmentService
{
    private readonly IApplicationDbContext _db;

    public DepartmentService(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<Department>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var query = _db.Departments.AsNoTracking().Where(d => !d.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => d.Name.Contains(search));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Department> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<Department>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await _db.Departments.AsNoTracking().Where(d => !d.IsDeleted && d.IsActive).OrderBy(d => d.Name).ToListAsync(cancellationToken);

    public async Task<Department?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Departments.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public async Task<ServiceResult<Department>> CreateAsync(DepartmentInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return ServiceResult<Department>.Fail("Departman adı zorunludur.", "VALIDATION");

        var duplicate = await _db.Departments.AnyAsync(d => d.Name == input.Name && !d.IsDeleted, cancellationToken);
        if (duplicate)
            return ServiceResult<Department>.Fail("Aynı isimde bir departman zaten var.", "DUPLICATE");

        var entity = new Department { Name = input.Name.Trim(), Description = input.Description, IsActive = input.IsActive };
        _db.Departments.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<Department>.Ok(entity);
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, DepartmentInput input, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(id, cancellationToken);
        if (entity is null)
            return ServiceResult.Fail("Departman bulunamadı.", "NOT_FOUND");

        entity.Name = input.Name.Trim();
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(id, cancellationToken);
        if (entity is null)
            return ServiceResult.Fail("Departman bulunamadı.", "NOT_FOUND");

        var hasInterns = await _db.InternProfiles.AnyAsync(i => i.DepartmentId == id && !i.IsDeleted, cancellationToken);
        if (hasInterns)
            return ServiceResult.Fail("Bu departmana bağlı stajyerler olduğu için silinemez. Önce pasife alın.", "IN_USE");

        entity.IsDeleted = true; // soft delete
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}
