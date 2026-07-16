using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class AttendanceCorrectionRequest : AuditableEntity
{
    public Guid AttendanceDayId { get; set; }
    public AttendanceDay? AttendanceDay { get; set; }

    public Guid RequestedByUserId { get; set; }
    public DateTime? RequestedCheckInUtc { get; set; }
    public DateTime? RequestedCheckOutUtc { get; set; }
    public string Reason { get; set; } = string.Empty;

    public CorrectionRequestStatus Status { get; set; } = CorrectionRequestStatus.Pending;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewerNote { get; set; }
}
