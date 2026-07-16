using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.DailyReports;

public interface IDailyReportService
{
    Task<ServiceResult<DailyReport>> CreateAsync(Guid userId, CreateDailyReportCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(Guid userId, UpdateDailyReportCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult<DailyWorkItem>> AddWorkItemAsync(Guid userId, AddWorkItemCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> RemoveWorkItemAsync(Guid userId, Guid reportId, Guid workItemId, CancellationToken cancellationToken = default);
    Task<ServiceResult> SubmitAsync(Guid userId, Guid reportId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ReviewAsync(Guid mentorUserId, ReviewDailyReportCommand command, CancellationToken cancellationToken = default);

    /// <summary>Stajyerin kendi raporunu getirir (başka stajyerin raporuna erişilemez).</summary>
    Task<ServiceResult<DailyReport>> GetForInternAsync(Guid userId, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>Mentorun yalnızca kendi stajyerinin raporunu getirir.</summary>
    Task<ServiceResult<DailyReport>> GetForMentorAsync(Guid mentorUserId, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>Stajyerin kendi raporlarını listeler.</summary>
    Task<IReadOnlyList<DailyReport>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Mentora atanmış stajyerlerin raporlarını (isteğe bağlı duruma göre) listeler.</summary>
    Task<IReadOnlyList<DailyReport>> ListForMentorAsync(Guid mentorUserId, DailyReportStatus? status, CancellationToken cancellationToken = default);
}
