using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Departments;
using Staj360.Application.Services.Internships;
using Staj360.Application.Services.Projects;
using Staj360.Domain.Enums;
using Staj360.Web.Areas.Mentor.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class ProjectsController : Controller
{
    private readonly IProjectService _projects;
    private readonly IProjectTaskService _tasks;
    private readonly IDepartmentService _departments;
    private readonly IInternService _interns;

    public ProjectsController(IProjectService projects, IProjectTaskService tasks, IDepartmentService departments, IInternService interns)
    {
        _projects = projects;
        _tasks = tasks;
        _departments = departments;
        _interns = interns;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var projects = await _projects.ListForMentorAsync(User.GetUserId(), cancellationToken);
        return View(projects);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new CreateProjectViewModel { Departments = await GetDepartmentsAsync(cancellationToken) };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Departments = await GetDepartmentsAsync(cancellationToken);
            return View(model);
        }

        var command = new CreateProjectCommand(model.Name, model.Description, model.StartDate, model.EndDate, model.DepartmentId, User.GetUserId(), model.RepositoryUrl);
        var result = await _projects.CreateAsync(command, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            model.Departments = await GetDepartmentsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Proje oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projects.GetAsync(id, cancellationToken);
        if (project is null || project.MentorUserId != User.GetUserId()) return NotFound();

        var vm = new ProjectDetailsViewModel
        {
            Project = project,
            Tasks = await _tasks.ListForProjectAsync(id, cancellationToken),
            Interns = await GetInternsAsync(cancellationToken)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProgress(Guid id, int progressPercentage, ProjectStatus status, CancellationToken cancellationToken)
    {
        var result = await _projects.UpdateProgressAsync(User.GetUserId(), false, id, progressPercentage, status, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "İlerleme güncellendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AssignIntern(Guid id, Guid internProfileId, string? roleDescription, CancellationToken cancellationToken)
    {
        var result = await _projects.AssignInternAsync(User.GetUserId(), false, id, internProfileId, roleDescription, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Stajyer projeye atandı." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateTaskViewModel model, CancellationToken cancellationToken)
    {
        var command = new CreateProjectTaskCommand(model.ProjectId, model.AssignedInternProfileId, model.Title, model.Description, model.Priority, model.DueDate, model.EstimatedMinutes);
        var result = await _tasks.CreateAsync(User.GetUserId(), false, command, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Görev oluşturuldu." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = model.ProjectId });
    }

    private async Task<List<SelectListItem>> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var departments = await _departments.ListActiveAsync(cancellationToken);
        return departments.Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToList();
    }

    private async Task<List<SelectListItem>> GetInternsAsync(CancellationToken cancellationToken)
    {
        var interns = await _interns.ListForMentorAsync(User.GetUserId(), cancellationToken);
        return interns.Select(i => new SelectListItem(i.StudentNumber, i.Id.ToString())).ToList();
    }
}
