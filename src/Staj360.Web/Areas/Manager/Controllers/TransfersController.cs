using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Organization;
using Staj360.Application.Services.Transfers;
using Staj360.Web.Areas.Manager.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = AppPolicies.ManagerArea)]
public class TransfersController : Controller
{
    private readonly IInternTransferService _transfers;
    private readonly IUnitAssignmentService _assignments;
    private readonly IOrganizationUnitService _units;
    private readonly IUserAccountService _accounts;
    private readonly IApplicationDbContext _db;

    public TransfersController(
        IInternTransferService transfers,
        IUnitAssignmentService assignments,
        IOrganizationUnitService units,
        IUserAccountService accounts,
        IApplicationDbContext db)
    {
        _transfers = transfers;
        _assignments = assignments;
        _units = units;
        _accounts = accounts;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _transfers.ListPendingForManagerAsync(User.GetUserId(), cancellationToken);
        ViewBag.Advisors = await GetAllAdvisorsAsync(cancellationToken);
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
            model.TargetAdvisorUserId,
            model.ExecuteImmediatelyIfSameManager);

        var result = await _transfers.CreateAsync(
            User.GetUserId(), isAdmin: false, isManager: true, isMentor: false, command, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Transfer oluşturulamadı.");
            return View(await BuildCreateVmAsync(model, cancellationToken));
        }

        TempData["Success"] = model.ExecuteImmediatelyIfSameManager
            ? "Transfer uygulandı."
            : "Transfer talebi oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(Guid transferRequestId, bool approve, Guid? targetAdvisorUserId, string? decisionNote, CancellationToken cancellationToken)
    {
        var command = new DecideTransferCommand(transferRequestId, approve, targetAdvisorUserId, decisionNote, null);
        var result = await _transfers.DecideAsync(User.GetUserId(), isAdmin: false, command, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? (approve ? "Transfer onaylandı." : "Transfer reddedildi.")
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    private async Task<CreateTransferViewModel> BuildCreateVmAsync(CreateTransferViewModel model, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var unitIds = await _assignments.GetManagerUnitIdsAsync(userId, cancellationToken);

        var interns = unitIds.Count == 0
            ? []
            : await _db.InternProfiles.AsNoTracking()
                .Include(i => i.CurrentOrganizationUnit)
                .Where(i => !i.IsDeleted && unitIds.Contains(i.CurrentOrganizationUnitId))
                .OrderBy(i => i.StudentNumber)
                .ToListAsync(cancellationToken);

        model.Interns = interns
            .Select(i => new SelectListItem(
                $"{i.StudentNumber} — {i.CurrentOrganizationUnit?.Name ?? "?"}",
                i.Id.ToString()))
            .ToList();

        var branches = await _units.ListBranchesAsync(cancellationToken);
        model.Branches = branches
            .Select(b => new SelectListItem(b.ParentName is null ? b.Name : $"{b.ParentName} / {b.Name}", b.Id.ToString()))
            .ToList();

        model.Advisors = await GetAllAdvisorsAsync(cancellationToken);
        return model;
    }

    private async Task<List<SelectListItem>> GetAllAdvisorsAsync(CancellationToken cancellationToken)
    {
        var mentors = await _accounts.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        return mentors.Select(m => new SelectListItem($"{m.FullName} ({m.Email})", m.UserId.ToString())).ToList();
    }
}
