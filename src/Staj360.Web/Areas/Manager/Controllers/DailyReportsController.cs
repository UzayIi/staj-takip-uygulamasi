using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.DailyReports;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = AppPolicies.ManagerArea)]
public class DailyReportsController : Controller
{
    private readonly IDailyReportService _reports;
    private readonly IUnitAssignmentService _assignments;
    private readonly IUserDisplayLookup _users;

    public DailyReportsController(
        IDailyReportService reports,
        IUnitAssignmentService assignments,
        IUserDisplayLookup users)
    {
        _reports = reports;
        _assignments = assignments;
        _users = users;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var items = await _reports.ListForManagerUnitsAsync(unitIds, cancellationToken);
        var userIds = items
            .Select(r => r.InternshipPeriod?.InternProfile?.UserId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct();
        var names = await _users.GetByIdsAsync(userIds, cancellationToken);
        ViewBag.InternNames = names.ToDictionary(kv => kv.Key, kv => kv.Value.FullName);
        return View(items);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var result = await _reports.GetForManagerAsync(unitIds, id, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return result.ErrorCode == "FORBIDDEN" ? Forbid() : RedirectToAction(nameof(Index));
        }

        var userId = result.Data!.InternshipPeriod?.InternProfile?.UserId;
        if (userId.HasValue)
        {
            var names = await _users.GetByIdsAsync(new[] { userId.Value }, cancellationToken);
            ViewBag.InternFullName = names.TryGetValue(userId.Value, out var info) ? info.FullName : null;
        }

        return View(result.Data);
    }
}
