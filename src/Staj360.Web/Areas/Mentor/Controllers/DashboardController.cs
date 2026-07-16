using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.DailyReports;
using Staj360.Application.Services.Internships;
using Staj360.Application.Services.Projects;
using Staj360.Domain.Enums;
using Staj360.Web.Areas.Mentor.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class DashboardController : Controller
{
    private readonly IInternService _internService;
    private readonly IDailyReportService _reportService;
    private readonly IProjectTaskService _taskService;
    private readonly IInternshipPeriodService _periodService;

    public DashboardController(
        IInternService internService,
        IDailyReportService reportService,
        IProjectTaskService taskService,
        IInternshipPeriodService periodService)
    {
        _internService = internService;
        _reportService = reportService;
        _taskService = taskService;
        _periodService = periodService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var interns = await _internService.ListForMentorAsync(userId, cancellationToken);
        var pending = await _reportService.ListForMentorAsync(userId, DailyReportStatus.Submitted, cancellationToken);
        var periods = await _periodService.ListForMentorAsync(userId, cancellationToken);

        var vm = new MentorDashboardViewModel
        {
            InternCount = interns.Count,
            PendingReportCount = pending.Count,
            ActivePeriodCount = periods.Count(p => p.Status == InternshipStatus.Active),
            RecentReports = pending.Take(10).ToList()
        };
        return View(vm);
    }
}
