using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Leaves;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class LeaveRequestsController : Controller
{
    private readonly ILeaveRequestService _leaves;

    public LeaveRequestsController(ILeaveRequestService leaves) => _leaves = leaves;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _leaves.ListForMentorViewAsync(User.GetUserId(), cancellationToken);
        return View(items);
    }
}
