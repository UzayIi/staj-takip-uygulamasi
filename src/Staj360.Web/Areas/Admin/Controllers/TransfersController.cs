using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Internships;
using Staj360.Application.Services.Organization;
using Staj360.Application.Services.Transfers;
using Staj360.Web.Areas.Admin.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class TransfersController : Controller
{
    private readonly IInternTransferService _transfers;
    private readonly IInternService _interns;
    private readonly IOrganizationUnitService _units;
    private readonly IUserAccountService _accounts;
    private readonly IApplicationDbContext _db;

    public TransfersController(
        IInternTransferService transfers,
        IInternService interns,
        IOrganizationUnitService units,
        IUserAccountService accounts,
        IApplicationDbContext db)
    {
        _transfers = transfers;
        _interns = interns;
        _units = units;
        _accounts = accounts;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _transfers.ListAllAsync(cancellationToken);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildCreateVmAsync(new CreateTransferViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTransferViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(await BuildCreateVmAsync(model, cancellationToken));

        var command = new CreateTransferCommand(
            model.InternProfileId,
            model.TargetOrganizationUnitId,
            model.RequestNote,
            model.TargetAdvisorUserId);

        var result = await _transfers.CreateAsync(User.GetUserId(), isAdmin: true, isManager: false, isMentor: false, command, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Transfer oluşturulamadı.");
            return View(await BuildCreateVmAsync(model, cancellationToken));
        }

        TempData["Success"] = "Transfer tamamlandı.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CreateTransferViewModel> BuildCreateVmAsync(CreateTransferViewModel model, CancellationToken cancellationToken)
    {
        var interns = await _interns.ListAsync(null, null, null, null, 1, 500, cancellationToken);
        model.Interns = interns.Items
            .Select(i => new SelectListItem($"{i.FullName} ({i.StudentNumber}) — {i.OrganizationUnitName}", i.InternProfileId.ToString()))
            .ToList();

        var branches = await _units.ListBranchesAsync(cancellationToken);
        model.Branches = branches
            .Select(b => new SelectListItem(b.ParentName is null ? b.Name : $"{b.ParentName} / {b.Name}", b.Id.ToString()))
            .ToList();

        // Hedef birime atanmış danışmanlar; yoksa tüm danışmanlar.
        if (model.TargetOrganizationUnitId != Guid.Empty)
        {
            var advisorIds = await _db.AdvisorUnitAssignments.AsNoTracking()
                .Where(a => a.OrganizationUnitId == model.TargetOrganizationUnitId && a.IsActive && !a.IsDeleted)
                .Select(a => a.AdvisorUserId)
                .ToListAsync(cancellationToken);
            var mentors = await _accounts.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
            model.Advisors = mentors
                .Where(m => advisorIds.Contains(m.UserId) || advisorIds.Count == 0)
                .Select(m => new SelectListItem($"{m.FullName} ({m.Email})", m.UserId.ToString()))
                .ToList();
        }
        else
        {
            var mentors = await _accounts.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
            model.Advisors = mentors.Select(m => new SelectListItem($"{m.FullName} ({m.Email})", m.UserId.ToString())).ToList();
        }

        return model;
    }
}
