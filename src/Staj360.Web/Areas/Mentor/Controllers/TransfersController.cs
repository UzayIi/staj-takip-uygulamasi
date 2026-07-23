using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Internships;
using Staj360.Application.Services.Organization;
using Staj360.Application.Services.Transfers;
using Staj360.Web.Areas.Mentor.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class TransfersController : Controller
{
    private readonly IInternTransferService _transfers;
    private readonly IInternService _interns;
    private readonly IOrganizationUnitService _units;

    public TransfersController(
        IInternTransferService transfers,
        IInternService interns,
        IOrganizationUnitService units)
    {
        _transfers = transfers;
        _interns = interns;
        _units = units;
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildVmAsync(new CreateTransferRequestViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTransferRequestViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(await BuildVmAsync(model, cancellationToken));

        var command = new CreateTransferCommand(
            model.InternProfileId,
            model.TargetOrganizationUnitId,
            model.RequestNote);

        var result = await _transfers.CreateAsync(
            User.GetUserId(), isAdmin: false, isManager: false, isMentor: true, command, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Transfer talebi oluşturulamadı.");
            return View(await BuildVmAsync(model, cancellationToken));
        }

        TempData["Success"] = "Transfer talebi oluşturuldu.";
        return RedirectToAction(nameof(Create));
    }

    private async Task<CreateTransferRequestViewModel> BuildVmAsync(CreateTransferRequestViewModel model, CancellationToken cancellationToken)
    {
        var interns = await _interns.ListForMentorAsync(User.GetUserId(), cancellationToken);
        model.Interns = interns.Select(i => new SelectListItem(i.StudentNumber, i.Id.ToString())).ToList();

        var branches = await _units.ListBranchesAsync(cancellationToken);
        model.Branches = branches
            .Select(b => new SelectListItem(b.ParentName is null ? b.Name : $"{b.ParentName} / {b.Name}", b.Id.ToString()))
            .ToList();
        return model;
    }
}
