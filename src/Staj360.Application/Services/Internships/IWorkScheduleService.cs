using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Internships;

public record WorkScheduleInput(
    string Name, TimeOnly StartTime, TimeOnly EndTime, int GracePeriodMinutes,
    bool Monday, bool Tuesday, bool Wednesday, bool Thursday, bool Friday, bool Saturday, bool Sunday);

public interface IWorkScheduleService
{
    Task<IReadOnlyList<WorkSchedule>> ListAsync(CancellationToken cancellationToken = default);
    Task<WorkSchedule?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkSchedule>> CreateAsync(WorkScheduleInput input, CancellationToken cancellationToken = default);
}
