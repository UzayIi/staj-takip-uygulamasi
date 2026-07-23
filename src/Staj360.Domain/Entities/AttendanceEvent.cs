using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class AttendanceEvent : AuditableEntity
{
    public Guid AttendanceDayId { get; set; }
    public AttendanceDay? AttendanceDay { get; set; }

    public AttendanceEventType EventType { get; set; }

    // Zaman sunucudan alınır, kullanıcı gönderdiği saatten değil.
    public DateTime EventTimeUtc { get; set; }

    public AttendanceSource Source { get; set; } = AttendanceSource.WebButton;
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Notes { get; set; }
}
