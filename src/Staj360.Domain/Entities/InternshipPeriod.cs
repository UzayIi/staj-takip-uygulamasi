using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class InternshipPeriod : AuditableEntity
{
    public Guid InternProfileId { get; set; }
    public InternProfile? InternProfile { get; set; }

    // Atanan danışman (Identity kullanıcısı).
    public Guid MentorUserId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public int RequiredWorkDays { get; set; }
    public int CompletedWorkDays { get; set; }

    public InternshipStatus Status { get; set; } = InternshipStatus.Pending;

    public Guid WorkScheduleId { get; set; }
    public WorkSchedule? WorkSchedule { get; set; }

    public ICollection<AttendanceDay> AttendanceDays { get; set; } = new List<AttendanceDay>();
    public ICollection<DailyReport> DailyReports { get; set; } = new List<DailyReport>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
}
