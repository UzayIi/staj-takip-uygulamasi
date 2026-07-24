using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Internships;
using Staj360.Application.Services.Projects;
using Staj360.Domain.Enums;
using Staj360.Web.Areas.Intern.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class DashboardController : Controller
{
    private readonly IInternshipPeriodService _periods;
    private readonly IProjectTaskService _tasks;
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ITimeZoneService _tz;

    public DashboardController(
        IInternshipPeriodService periods,
        IProjectTaskService tasks,
        IApplicationDbContext db,
        IClock clock,
        ITimeZoneService tz)
    {
        _periods = periods;
        _tasks = tasks;
        _db = db;
        _clock = clock;
        _tz = tz;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var vm = new InternDashboardViewModel();

        var period = await _periods.GetActiveForUserAsync(userId, cancellationToken);
        if (period is not null)
        {
            vm.HasActivePeriod = true;
            var today = _tz.LocalDate(_clock.UtcNow);
            vm.RemainingDays = Math.Max(0, period.EndDate.DayNumber - today.DayNumber);

            var report = await _db.DailyReports.AsNoTracking()
                .FirstOrDefaultAsync(r => r.InternshipPeriodId == period.Id && r.ReportDate == today && !r.IsDeleted, cancellationToken);
            vm.HasTodayReport = report is not null;
            vm.TodayReportStatus = report?.Status;
        }

        var tasks = await _tasks.ListForInternAsync(userId, cancellationToken);
        vm.ActiveTaskCount = tasks.Count(t => t.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress);

        return View(vm);
    }
}
