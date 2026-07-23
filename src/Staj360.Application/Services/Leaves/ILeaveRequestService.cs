using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Leaves;

public record CreateLeaveRequestCommand(
    LeaveType LeaveType, DateOnly StartDate, DateOnly EndDate, string Reason, string? DocumentPath);

public record ReviewLeaveCommand(Guid LeaveRequestId, bool Approve, string? ReviewerNote, byte[]? RowVersion = null);

public class LeaveCreateResult
{
    public LeaveRequest Request { get; init; } = default!;
    public bool HasOverlapWarning { get; init; }
}

public interface ILeaveRequestService
{
    Task<ServiceResult<LeaveCreateResult>> CreateAsync(Guid userId, CreateLeaveRequestCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveRequest>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveRequest>> ListPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveRequest>> ListPendingForManagerAsync(Guid managerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveRequest>> ListForMentorViewAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
    /// <summary>Yalnızca ilgili birimin aktif yöneticileri onaylayabilir. Admin/danışman onaylayamaz.</summary>
    Task<ServiceResult> ReviewAsync(Guid reviewerUserId, ReviewLeaveCommand command, CancellationToken cancellationToken = default);
}
