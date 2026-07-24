using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Leaves;
using Staj360.Application.Services.Projects;
using Staj360.Application.Services.Transfers;
using Staj360.Domain.Enums;
using Staj360.Web.Areas.Manager.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = AppPolicies.ManagerArea)]
public class DashboardController : Controller
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitAssignmentService _assignments;
    private readonly ILeaveRequestService _leaves;
    private readonly IInternTransferService _transfers;
    private readonly IProjectService _projects;
    private readonly IClock _clock;

    public DashboardController(
        IApplicationDbContext db,
        IUnitAssignmentService assignments,
        ILeaveRequestService leaves,
        IInternTransferService transfers,
        IProjectService projects,
        IClock clock)
    {
        _db = db;
        _assignments = assignments;
        _leaves = leaves;
        _transfers = transfers;
        _projects = projects;
        _clock = clock;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var unitIds = await _assignments.GetManagerUnitIdsAsync(userId, cancellationToken);

        var internCount = unitIds.Count == 0
            ? 0
            : await _db.InternProfiles.AsNoTracking()
                .CountAsync(i => !i.IsDeleted && i.IsActive && unitIds.Contains(i.CurrentOrganizationUnitId), cancellationToken);

        var pendingLeaves = await _leaves.ListPendingForManagerAsync(userId, cancellationToken);
        var pendingTransfers = await _transfers.ListPendingForManagerAsync(userId, cancellationToken);
        var projects = await _projects.ListForManagerUnitsAsync(unitIds, cancellationToken);

        var projectIds = projects.Select(p => p.Id).ToList();
        var activeProjects = projects.Count(p => p.Status is ProjectStatus.InProgress or ProjectStatus.Planned);

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var overdueTasks = 0;
        var completedTasks = 0;
        var assignedInterns = 0;
        var recent = new List<ManagerDashboardActivityItem>();

        if (projectIds.Count > 0)
        {
            overdueTasks = await _db.ProjectTasks.AsNoTracking()
                .CountAsync(t => projectIds.Contains(t.ProjectId) && !t.IsDeleted
                    && t.Status != ProjectTaskStatus.Done
                    && t.DueDate != null && t.DueDate < today, cancellationToken);

            completedTasks = await _db.ProjectTasks.AsNoTracking()
                .CountAsync(t => projectIds.Contains(t.ProjectId) && !t.IsDeleted && t.Status == ProjectTaskStatus.Done, cancellationToken);

            assignedInterns = await _db.ProjectAssignments.AsNoTracking()
                .CountAsync(a => projectIds.Contains(a.ProjectId) && a.IsActive && !a.IsDeleted, cancellationToken);

            var recentProjects = projects.OrderByDescending(p => p.UpdatedAtUtc ?? p.CreatedAtUtc).Take(5)
                .Select(p => new ManagerDashboardActivityItem
                {
                    Title = p.Name,
                    Detail = $"Proje · {DisplayStatus(p.Status)}",
                    AtUtc = p.UpdatedAtUtc ?? p.CreatedAtUtc
                });
            recent.AddRange(recentProjects);
        }

        var vm = new ManagerDashboardViewModel
        {
            InternCount = internCount,
            PendingLeaveCount = pendingLeaves.Count,
            PendingTransferCount = pendingTransfers.Count,
            UnitCount = unitIds.Count,
            ActiveProjectCount = activeProjects,
            OverdueTaskCount = overdueTasks,
            CompletedTaskCount = completedTasks,
            AssignedInternCount = assignedInterns,
            RecentActivity = recent.OrderByDescending(a => a.AtUtc).Take(8).ToList()
        };
        return View(vm);
    }

    private static string DisplayStatus(ProjectStatus status) => status switch
    {
        ProjectStatus.Planned => "Planlandı",
        ProjectStatus.InProgress => "Devam ediyor",
        ProjectStatus.Completed => "Tamamlandı",
        ProjectStatus.OnHold => "Beklemede",
        ProjectStatus.Cancelled => "İptal",
        _ => status.ToString()
    };
}
