using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Organization;
using Staj360.Application.Services.Projects;
using Staj360.Web.Areas.Admin.Models;
using Staj360.Web.Helpers;
using Staj360.Web.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class ProjectsController : Controller
{
    private const int PageSize = 20;
    private readonly IProjectService _projects;
    private readonly IProjectDetailsService _details;
    private readonly IOrganizationUnitService _units;
    private readonly IUserAccountService _accounts;

    public ProjectsController(
        IProjectService projects,
        IProjectDetailsService details,
        IOrganizationUnitService units,
        IUserAccountService accounts)
    {
        _projects = projects;
        _details = details;
        _units = units;
        _accounts = accounts;
    }

    public async Task<IActionResult> Index(int? year, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _projects.ListAsync(year, page, PageSize, cancellationToken);
        ViewData["Year"] = year;
        ViewData["Years"] = await _projects.GetAvailableYearsAsync(cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _details.GetDetailsAsync(id, User.GetUserId(), ProjectViewerKind.Admin, cancellationToken);
        if (!result.Success)
            return result.ErrorCode == "FORBIDDEN" ? Forbid() : NotFound();

        return View(new ProjectDetailsPageViewModel
        {
            Details = result.Data!,
            ShowManagePanel = result.Data!.CanManage,
            AreaName = "Admin",
            IndexController = "Projects",
            ReportDetailsArea = "Admin",
            ReportDetailsController = "DailyReports",
            ReportDetailsAction = "Details"
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new CreateProjectViewModel
        {
            OrganizationUnits = await GetUnitsAsync(cancellationToken),
            Mentors = await GetMentorsAsync(cancellationToken)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.OrganizationUnits = await GetUnitsAsync(cancellationToken);
            model.Mentors = await GetMentorsAsync(cancellationToken);
            return View(model);
        }

        var command = new CreateProjectCommand(model.Name, model.Description, model.StartDate, model.EndDate, model.OrganizationUnitId, model.MentorUserId, model.RepositoryUrl);
        var result = await _projects.CreateAsync(command, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            model.OrganizationUnits = await GetUnitsAsync(cancellationToken);
            model.Mentors = await GetMentorsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Proje oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    private async Task<List<SelectListItem>> GetUnitsAsync(CancellationToken cancellationToken)
    {
        var branches = await _units.ListBranchesAsync(cancellationToken);
        return branches.Select(d => new SelectListItem(
            d.ParentName is null ? d.Name : $"{d.ParentName} / {d.Name}",
            d.Id.ToString())).ToList();
    }

    private async Task<List<SelectListItem>> GetMentorsAsync(CancellationToken cancellationToken)
    {
        var mentors = await _accounts.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        return mentors.Select(m => new SelectListItem($"{m.FullName} ({m.Email})", m.UserId.ToString())).ToList();
    }
}
