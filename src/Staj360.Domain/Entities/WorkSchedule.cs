using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

public class WorkSchedule : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int GracePeriodMinutes { get; set; }

    public bool MondayEnabled { get; set; }
    public bool TuesdayEnabled { get; set; }
    public bool WednesdayEnabled { get; set; }
    public bool ThursdayEnabled { get; set; }
    public bool FridayEnabled { get; set; }
    public bool SaturdayEnabled { get; set; }
    public bool SundayEnabled { get; set; }

    /// <summary>Belirtilen güne çalışma programında izin veriliyor mu?</summary>
    public bool IsWorkingDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => MondayEnabled,
        DayOfWeek.Tuesday => TuesdayEnabled,
        DayOfWeek.Wednesday => WednesdayEnabled,
        DayOfWeek.Thursday => ThursdayEnabled,
        DayOfWeek.Friday => FridayEnabled,
        DayOfWeek.Saturday => SaturdayEnabled,
        DayOfWeek.Sunday => SundayEnabled,
        _ => false
    };
}
