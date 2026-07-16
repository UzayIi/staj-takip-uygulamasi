using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Internships;

public interface IInternService
{
    Task<PagedResult<InternListItemDto>> ListAsync(string? search, int? year, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<InternProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InternProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternProfile>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
}
