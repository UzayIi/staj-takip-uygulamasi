using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Projects;
using Staj360.Web.Helpers;
using Staj360.Web.Models;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyProjectsController : Controller
{
    private const int PageSize = 20;
    private readonly IProjectService _projects;
    private readonly IProjectDetailsService _details;

    public MyProjectsController(IProjectService projects, IProjectDetailsService details)
    {
        _projects = projects;
        _details = details;
    }

    public async Task<IActionResult> Index(int? year, int page = 1, CancellationToken cancellationToken = default)
    {
        // Stajyer tüm projeleri okuyabilir; liste genel projelerden oluşur.
        var projects = await _projects.ListAsync(year, page, PageSize, cancellationToken);
        ViewData["Year"] = year;
        ViewData["Years"] = await _projects.GetAvailableYearsAsync(cancellationToken);
        return View(projects);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _details.GetDetailsAsync(id, User.GetUserId(), ProjectViewerKind.Intern, cancellationToken);
        if (!result.Success)
            return result.ErrorCode == "FORBIDDEN" ? Forbid() : NotFound();

        return View(new ProjectDetailsPageViewModel
        {
            Details = result.Data!,
            ShowManagePanel = false,
            AreaName = "Intern",
            IndexController = "MyProjects",
            ReportDetailsArea = "Intern",
            ReportDetailsController = "TeamWork",
            ReportDetailsAction = "ReportDetails"
        });
    }

    /// <summary>Stajyer proje düzenleyemez — URL ile denense bile 403.</summary>
    [HttpPost]
    public IActionResult UpdateProgress(Guid id) => Forbid();

    [HttpPost]
    public IActionResult AssignIntern(Guid id) => Forbid();

    [HttpPost]
    public IActionResult CreateTask(Guid id) => Forbid();
}
