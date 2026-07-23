using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Departments;

public record DepartmentInput(string Name, string? Description, bool IsActive);

public interface IDepartmentService
{
    Task<PagedResult<Department>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<Department?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<Department>> CreateAsync(DepartmentInput input, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(Guid id, DepartmentInput input, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
