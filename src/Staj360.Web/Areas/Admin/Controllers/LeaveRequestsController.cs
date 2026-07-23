using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Leaves;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class LeaveRequestsController : Controller
{
    private readonly ILeaveRequestService _leaves;

    public LeaveRequestsController(ILeaveRequestService leaves) => _leaves = leaves;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Admin yalnızca görüntüleyebilir; onay Yönetici (Manager) tarafından yapılır.
        var items = await _leaves.ListPendingAsync(cancellationToken);
        return View(items);
    }
}
