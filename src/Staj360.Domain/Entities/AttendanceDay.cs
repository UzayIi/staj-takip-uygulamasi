using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class AttendanceDay : AuditableEntity
{
    public Guid InternshipPeriodId { get; set; }
    public InternshipPeriod? InternshipPeriod { get; set; }

    // İş günü yerel tarih ile belirlenir.
    public DateOnly WorkDate { get; set; }

    public DateTime? FirstCheckInUtc { get; set; }
    public DateTime? LastCheckOutUtc { get; set; }
    public int TotalWorkedMinutes { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.NotStarted;
    public bool IsLate { get; set; }
    public bool IsEarlyLeave { get; set; }
    public bool IsIncomplete { get; set; }

    public ICollection<AttendanceEvent> Events { get; set; } = new List<AttendanceEvent>();
}
