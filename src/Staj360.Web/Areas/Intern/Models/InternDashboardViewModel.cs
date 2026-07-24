using Staj360.Domain.Enums;

namespace Staj360.Web.Areas.Intern.Models;

public class InternDashboardViewModel
{
    public bool HasActivePeriod { get; set; }
    public int RemainingDays { get; set; }
    public bool HasTodayReport { get; set; }
    public DailyReportStatus? TodayReportStatus { get; set; }
    public int ActiveTaskCount { get; set; }
    public string? LastMentorComment { get; set; }
}
