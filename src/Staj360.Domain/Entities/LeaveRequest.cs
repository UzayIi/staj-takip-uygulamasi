using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class LeaveRequest : AuditableEntity
{
    public Guid InternshipPeriodId { get; set; }
    public InternshipPeriod? InternshipPeriod { get; set; }

    public LeaveType LeaveType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? DocumentPath { get; set; }

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewerNote { get; set; }
}
