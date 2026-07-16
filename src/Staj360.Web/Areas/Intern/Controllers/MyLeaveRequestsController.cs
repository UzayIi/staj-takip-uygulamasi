using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Leaves;
using Staj360.Web.Areas.Intern.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyLeaveRequestsController : Controller
{
    private readonly ILeaveRequestService _leaves;

    public MyLeaveRequestsController(ILeaveRequestService leaves) => _leaves = leaves;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _leaves.ListForInternAsync(User.GetUserId(), cancellationToken);
        return View(items);
    }

    [HttpGet]
    public IActionResult Create() => View(new LeaveRequestViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(LeaveRequestViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var command = new CreateLeaveRequestCommand(model.LeaveType, model.StartDate, model.EndDate, model.Reason, null);
        var result = await _leaves.CreateAsync(User.GetUserId(), command, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return View(model);
        }

        if (result.Data!.HasOverlapWarning)
        {
            TempData["Warning"] = "İzin talebiniz oluşturuldu ancak mevcut bir izinle çakışıyor olabilir.";
        }
        else
        {
            TempData["Success"] = "İzin talebiniz oluşturuldu.";
        }
        return RedirectToAction(nameof(Index));
    }
}
