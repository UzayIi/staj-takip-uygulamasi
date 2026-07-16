using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Leaves;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class LeaveRequestsController : Controller
{
    private readonly ILeaveRequestService _leaves;

    public LeaveRequestsController(ILeaveRequestService leaves) => _leaves = leaves;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _leaves.ListPendingAsync(cancellationToken);
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Review(Guid leaveRequestId, bool approve, string? reviewerNote, CancellationToken cancellationToken)
    {
        var command = new ReviewLeaveCommand(leaveRequestId, approve, reviewerNote);
        var result = await _leaves.ReviewAsync(User.GetUserId(), true, command, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? (approve ? "İzin talebi onaylandı." : "İzin talebi reddedildi.")
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
