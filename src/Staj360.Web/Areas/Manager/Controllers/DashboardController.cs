using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Leaves;
using Staj360.Application.Services.Transfers;
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

    public DashboardController(
        IApplicationDbContext db,
        IUnitAssignmentService assignments,
        ILeaveRequestService leaves,
        IInternTransferService transfers)
    {
        _db = db;
        _assignments = assignments;
        _leaves = leaves;
        _transfers = transfers;
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

        var vm = new ManagerDashboardViewModel
        {
            InternCount = internCount,
            PendingLeaveCount = pendingLeaves.Count,
            PendingTransferCount = pendingTransfers.Count,
            UnitCount = unitIds.Count
        };
        return View(vm);
    }
}
