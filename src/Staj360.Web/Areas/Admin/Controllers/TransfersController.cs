using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Transfers;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class TransfersController : Controller
{
    private readonly IInternTransferService _transfers;

    public TransfersController(IInternTransferService transfers) => _transfers = transfers;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _transfers.ListAllAsync(cancellationToken);
        return View(items);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var item = await _transfers.GetAsync(id, cancellationToken);
        if (item is null)
            return NotFound();
        return View(item);
    }

    [HttpGet]
    public IActionResult Create() => Forbid();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(object _) => Forbid();
}
