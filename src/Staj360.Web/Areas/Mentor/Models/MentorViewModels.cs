using System.ComponentModel.DataAnnotations;
using Staj360.Application.Services.DailyReports;
using Staj360.Domain.Entities;

namespace Staj360.Web.Areas.Mentor.Models;

public class MentorDashboardViewModel
{
    public int InternCount { get; set; }
    public int PendingReportCount { get; set; }
    public int ActivePeriodCount { get; set; }
    public IReadOnlyList<DailyReport> RecentReports { get; set; } = Array.Empty<DailyReport>();
}

public class ReviewDailyReportViewModel
{
    public Guid ReportId { get; set; }

    [Required(ErrorMessage = "Karar seçiniz.")]
    [Display(Name = "Karar")]
    public ReviewDecision Decision { get; set; }

    [Display(Name = "Danışman Yorumu")]
    [StringLength(2000)]
    public string? MentorComment { get; set; }
}
