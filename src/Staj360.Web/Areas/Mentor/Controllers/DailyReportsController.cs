using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.DailyReports;
using Staj360.Web.Areas.Mentor.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class DailyReportsController : Controller
{
    private readonly IDailyReportService _service;
    private readonly IUserDisplayLookup _users;

    public DailyReportsController(IDailyReportService service, IUserDisplayLookup users)
    {
        _service = service;
        _users = users;
    }

    public async Task<IActionResult> Index(Staj360.Domain.Enums.DailyReportStatus? status, CancellationToken cancellationToken)
    {
        var reports = await _service.ListForMentorAsync(User.GetUserId(), status ?? Staj360.Domain.Enums.DailyReportStatus.Submitted, cancellationToken);
        var userIds = reports
            .Select(r => r.InternshipPeriod?.InternProfile?.UserId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct();
        var names = await _users.GetByIdsAsync(userIds, cancellationToken);
        ViewBag.InternNames = names.ToDictionary(kv => kv.Key, kv => kv.Value.FullName);
        ViewData["Status"] = status;
        return View(reports);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetForMentorAsync(User.GetUserId(), id, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        var userId = result.Data!.InternshipPeriod?.InternProfile?.UserId;
        if (userId.HasValue)
        {
            var names = await _users.GetByIdsAsync(new[] { userId.Value }, cancellationToken);
            ViewBag.InternFullName = names.TryGetValue(userId.Value, out var info) ? info.FullName : null;
        }

        return View(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Review(ReviewDailyReportViewModel model, CancellationToken cancellationToken)
    {
        var command = new ReviewDailyReportCommand(model.ReportId, model.Decision, model.MentorComment);
        var result = await _service.ReviewAsync(User.GetUserId(), command, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Rapor değerlendirmesi kaydedildi." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = model.ReportId });
    }
}
