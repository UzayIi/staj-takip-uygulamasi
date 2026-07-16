namespace Staj360.Web.Areas.Admin.Models;

public record AuditRow(string EntityName, string Action, DateTime CreatedAtUtc);

public class AdminDashboardViewModel
{
    public int ActiveInterns { get; set; }
    public int CheckedInToday { get; set; }
    public int LateToday { get; set; }
    public int IncompleteToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int PendingReports { get; set; }
    public int PendingLeaves { get; set; }
    public int OngoingProjects { get; set; }
    public int EndingSoon { get; set; }
    public IReadOnlyList<AuditRow> RecentAudits { get; set; } = Array.Empty<AuditRow>();
}
