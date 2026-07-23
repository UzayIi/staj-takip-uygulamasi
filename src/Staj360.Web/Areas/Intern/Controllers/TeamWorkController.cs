using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.TeamWork;
using Staj360.Domain.Enums;
using Staj360.Web.Areas.Intern.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

/// <summary>
/// Ekip Günlükleri: diğer stajyerlerin paylaşılabilir raporları (salt okunur).
/// Düzenleme / silme / onay endpoint'i yoktur.
/// </summary>
[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class TeamWorkController : Controller
{
    private const int PageSize = 10;
    private readonly ITeamWorkService _teamWork;

    public TeamWorkController(ITeamWorkService teamWork) => _teamWork = teamWork;

    [HttpGet]
    public async Task<IActionResult> Index(
        string tab = "reports",
        string? internName = null,
        Guid? projectId = null,
        ProjectStatus? status = null,
        DailyReportStatus? reportStatus = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        tab = string.Equals(tab, "projects", StringComparison.OrdinalIgnoreCase) ? "projects" : "reports";

        var projectOptions = (await _teamWork.ListProjectsForFilterAsync(cancellationToken))
            .Select(p => new SelectListItem(p.Name, p.Id.ToString(), projectId == p.Id))
            .ToList();

        var vm = new TeamWorkIndexViewModel
        {
            Tab = tab,
            InternName = internName,
            ProjectId = projectId,
            Status = status,
            ReportStatus = reportStatus,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            ProjectOptions = projectOptions
        };

        if (tab == "reports")
        {
            vm.Reports = await _teamWork.ListPeerReportsAsync(
                new TeamReportFilter(internName, projectId, fromDate, toDate, reportStatus),
                page, PageSize, cancellationToken);
        }
        else
        {
            vm.Projects = await _teamWork.ListPeerProjectsAsync(
                new TeamProjectFilter(internName, status, fromDate, toDate), page, PageSize, cancellationToken);
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ReportDetails(Guid id, CancellationToken cancellationToken)
    {
        var result = await _teamWork.GetPeerReportDetailAsync(id, User.GetUserId(), cancellationToken);
        if (!result.Success)
            return result.ErrorCode == "FORBIDDEN" ? Forbid() : NotFound();

        return View(result.Data);
    }

    [HttpPost]
    public IActionResult EditReport(Guid id) => Forbid();

    [HttpPost]
    public IActionResult DeleteReport(Guid id) => Forbid();

    [HttpPost]
    public IActionResult ReviewReport(Guid id) => Forbid();
}
