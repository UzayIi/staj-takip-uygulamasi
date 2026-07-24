using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Projects;
using Staj360.Domain.Enums;
using Staj360.Web.Helpers;
using Staj360.Web.Models;

namespace Staj360.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = AppPolicies.ManagerArea)]
public class ProjectsController : Controller
{
    private readonly IProjectService _projects;
    private readonly IProjectDetailsService _details;
    private readonly IProjectTaskService _tasks;
    private readonly IUnitAssignmentService _assignments;
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public ProjectsController(
        IProjectService projects,
        IProjectDetailsService details,
        IProjectTaskService tasks,
        IUnitAssignmentService assignments,
        IApplicationDbContext db,
        IClock clock)
    {
        _projects = projects;
        _details = details;
        _tasks = tasks;
        _assignments = assignments;
        _db = db;
        _clock = clock;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var projects = await _projects.ListForManagerUnitsAsync(unitIds, cancellationToken);
        return View(projects);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _details.GetDetailsAsync(id, User.GetUserId(), ProjectViewerKind.Manager, cancellationToken);
        if (!result.Success)
            return result.ErrorCode == "FORBIDDEN" ? Forbid() : NotFound();

        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var vm = new ProjectDetailsPageViewModel
        {
            Details = result.Data!,
            ShowManagePanel = result.Data!.CanManage,
            AreaName = "Manager",
            ReportDetailsArea = "Manager",
            ReportDetailsController = "DailyReports",
            ReportDetailsAction = "Details",
            AssignableInterns = result.Data.CanManage ? await GetInternsForProjectAsync(id, unitIds, cancellationToken) : new()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgress(Guid id, int progressPercentage, ProjectStatus status, CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var result = await _projects.UpdateProgressAsync(
            User.GetUserId(), isAdmin: false, isManager: true, unitIds, id, progressPercentage, status, cancellationToken);
        if (!result.Success && result.ErrorCode == "FORBIDDEN")
            return Forbid();
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "İlerleme güncellendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignIntern(Guid id, Guid internProfileId, string? roleDescription, CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var result = await _projects.AssignInternAsync(
            User.GetUserId(), isAdmin: false, isManager: true, unitIds, id, internProfileId, roleDescription, cancellationToken);
        if (!result.Success && result.ErrorCode == "FORBIDDEN")
            return Forbid();
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Stajyer projeye atandı." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndAssignment(Guid id, Guid assignmentId, DateOnly? endDate, CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var localToday = DateOnly.FromDateTime(_clock.UtcNow); // fallback; UI gönderir
        var date = endDate ?? localToday;
        var result = await _projects.EndAssignmentAsync(
            User.GetUserId(), isAdmin: false, isManager: true, unitIds, id, assignmentId, date, cancellationToken);
        if (!result.Success && result.ErrorCode == "FORBIDDEN")
            return Forbid();
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Atama sonlandırıldı." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTask(
        Guid ProjectId, Guid? AssignedInternProfileId, string Title, string? Description,
        TaskPriority Priority, DateOnly? DueDate, int? EstimatedMinutes, CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var command = new CreateProjectTaskCommand(ProjectId, AssignedInternProfileId, Title, Description, Priority, DueDate, EstimatedMinutes);
        var result = await _tasks.CreateAsync(User.GetUserId(), isAdmin: false, isManager: true, unitIds, command, cancellationToken);
        if (!result.Success && result.ErrorCode == "FORBIDDEN")
            return Forbid();
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Görev oluşturuldu." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = ProjectId });
    }

    private async Task<List<SelectListItem>> GetInternsForProjectAsync(
        Guid projectId, IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null || !unitIds.Contains(project.OrganizationUnitId))
            return new List<SelectListItem>();

        var interns = await _db.InternProfiles.AsNoTracking()
            .Where(i => !i.IsDeleted && i.IsActive && i.CurrentOrganizationUnitId == project.OrganizationUnitId)
            .OrderBy(i => i.StudentNumber)
            .ToListAsync(cancellationToken);

        return interns.Select(i => new SelectListItem(i.StudentNumber, i.Id.ToString())).ToList();
    }
}
