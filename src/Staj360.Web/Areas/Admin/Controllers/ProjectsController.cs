using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Departments;
using Staj360.Application.Services.Projects;
using Staj360.Web.Areas.Admin.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class ProjectsController : Controller
{
    private const int PageSize = 20;
    private readonly IProjectService _projects;
    private readonly IDepartmentService _departments;
    private readonly IUserAccountService _accounts;

    public ProjectsController(IProjectService projects, IDepartmentService departments, IUserAccountService accounts)
    {
        _projects = projects;
        _departments = departments;
        _accounts = accounts;
    }

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _projects.ListAsync(page, PageSize, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new CreateProjectViewModel
        {
            Departments = await GetDepartmentsAsync(cancellationToken),
            Mentors = await GetMentorsAsync(cancellationToken)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Departments = await GetDepartmentsAsync(cancellationToken);
            model.Mentors = await GetMentorsAsync(cancellationToken);
            return View(model);
        }

        var command = new CreateProjectCommand(model.Name, model.Description, model.StartDate, model.EndDate, model.DepartmentId, model.MentorUserId, model.RepositoryUrl);
        var result = await _projects.CreateAsync(command, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            model.Departments = await GetDepartmentsAsync(cancellationToken);
            model.Mentors = await GetMentorsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Proje oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var departments = await _departments.ListActiveAsync(cancellationToken);
        return departments.Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToList();
    }

    private async Task<List<SelectListItem>> GetMentorsAsync(CancellationToken cancellationToken)
    {
        var mentors = await _accounts.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        return mentors.Select(m => new SelectListItem($"{m.FullName} ({m.Email})", m.UserId.ToString())).ToList();
    }
}
