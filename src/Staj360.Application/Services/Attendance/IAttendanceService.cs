using Staj360.Application.Common;

namespace Staj360.Application.Services.Attendance;

public interface IAttendanceService
{
    Task<ServiceResult<AttendanceDaySummary>> CheckInAsync(Guid userId, AttendanceActionContext context, CancellationToken cancellationToken = default);
    Task<ServiceResult<AttendanceDaySummary>> CheckOutAsync(Guid userId, AttendanceActionContext context, CancellationToken cancellationToken = default);
    Task<ServiceResult<AttendanceDaySummary>> GetTodayStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}
