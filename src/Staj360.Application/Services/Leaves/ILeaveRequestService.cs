using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Leaves;

public record CreateLeaveRequestCommand(
    LeaveType LeaveType, DateOnly StartDate, DateOnly EndDate, string Reason, string? DocumentPath);

public record ReviewLeaveCommand(Guid LeaveRequestId, bool Approve, string? ReviewerNote);

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
    Task<ServiceResult> ReviewAsync(Guid reviewerUserId, bool isAdmin, ReviewLeaveCommand command, CancellationToken cancellationToken = default);
}
