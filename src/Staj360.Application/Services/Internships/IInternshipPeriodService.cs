using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Internships;

public record CreateInternshipPeriodCommand(
    Guid InternProfileId,
    Guid MentorUserId,
    Guid WorkScheduleId,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequiredWorkDays);

public interface IInternshipPeriodService
{
    Task<PagedResult<InternshipPeriod>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<InternshipPeriod?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<InternshipPeriod>> CreateAsync(CreateInternshipPeriodCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternshipPeriod>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
    Task<InternshipPeriod?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
