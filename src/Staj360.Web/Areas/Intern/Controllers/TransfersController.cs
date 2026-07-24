using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Transfers;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class TransfersController : Controller
{
    private readonly IInternTransferService _transfers;

    public TransfersController(IInternTransferService transfers) => _transfers = transfers;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _transfers.ListCompletedForInternAsync(User.GetUserId(), cancellationToken);
        return View(items);
    }
}
