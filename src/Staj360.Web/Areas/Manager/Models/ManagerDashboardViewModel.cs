namespace Staj360.Web.Areas.Manager.Models;

public class ManagerDashboardViewModel
{
    public int InternCount { get; set; }
    public int PendingLeaveCount { get; set; }
    public int PendingTransferCount { get; set; }
    public int UnitCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int OverdueTaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int AssignedInternCount { get; set; }
    public IReadOnlyList<ManagerDashboardActivityItem> RecentActivity { get; set; } = Array.Empty<ManagerDashboardActivityItem>();
}

public class ManagerDashboardActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
}
