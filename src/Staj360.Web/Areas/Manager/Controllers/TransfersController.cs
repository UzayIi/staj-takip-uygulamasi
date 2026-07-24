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
using Staj360.Domain.Enums;
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
    private readonly IUserDisplayLookup _users;
    private readonly IApplicationDbContext _db;

    public TransfersController(
        IInternTransferService transfers,
        IUnitAssignmentService assignments,
        IOrganizationUnitService units,
        IUserAccountService accounts,
        IUserDisplayLookup users,
        IApplicationDbContext db)
    {
        _transfers = transfers;
        _assignments = assignments;
        _units = units;
        _accounts = accounts;
        _users = users;
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
        if (model.TargetDirectorateId == Guid.Empty)
            ModelState.AddModelError(nameof(model.TargetDirectorateId), "Önce hedef daire başkanlığını seçiniz.");

        if (ModelState.IsValid && model.TargetOrganizationUnitId != Guid.Empty)
        {
            var parentCheck = await _units.ValidateBranchBelongsToDirectorateAsync(
                model.TargetOrganizationUnitId, model.TargetDirectorateId, cancellationToken);
            if (!parentCheck.Success)
                ModelState.AddModelError(nameof(model.TargetOrganizationUnitId),
                    parentCheck.ErrorMessage ?? "Seçilen müdürlük bu daire başkanlığına bağlı değildir.");
        }

        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        var intern = await _db.InternProfiles.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == model.InternProfileId && !i.IsDeleted, cancellationToken);
        if (intern is null || !unitIds.Contains(intern.CurrentOrganizationUnitId))
            ModelState.AddModelError(nameof(model.InternProfileId), "Seçilen stajyer için transfer yetkiniz bulunmuyor.");

        if (!ModelState.IsValid)
            return View(await BuildCreateVmAsync(model, cancellationToken));

        var command = new CreateTransferCommand(
            model.InternProfileId,
            model.TargetOrganizationUnitId,
            model.RequestNote,
            model.PlannedStartDate,
            model.TargetAdvisorUserId,
            model.ExecuteImmediatelyIfSameManager);

        var result = await _transfers.CreateAsync(User.GetUserId(), command, cancellationToken);
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

    [HttpGet]
    public async Task<IActionResult> BranchesForDirectorate(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return Json(new { items = Array.Empty<object>(), message = "Önce hedef daire başkanlığını seçiniz." });

        var branches = await _units.ListBranchesByDirectorateAsync(id, cancellationToken);
        if (branches.Count == 0)
            return Json(new { items = Array.Empty<object>(), message = "Bu başkanlığa bağlı aktif müdürlük bulunamadı." });

        return Json(new
        {
            items = branches.Select(b => new { id = b.Id, name = b.Name }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> AdvisorsForBranch(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return Json(new { items = Array.Empty<object>(), message = "Önce hedef şube müdürlüğünü seçiniz." });

        var branch = await _units.GetAsync(id, cancellationToken);
        if (branch is null || branch.UnitType != OrganizationUnitType.Branch)
            return Json(new { items = Array.Empty<object>(), message = "Bu müdürlükte aktif danışman bulunamadı." });

        var advisorIds = await _db.AdvisorUnitAssignments.AsNoTracking()
            .Where(a => a.OrganizationUnitId == id && a.IsActive && !a.IsDeleted)
            .Select(a => a.AdvisorUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (advisorIds.Count == 0)
            return Json(new { items = Array.Empty<object>(), message = "Bu müdürlükte aktif danışman bulunamadı." });

        var map = await _users.GetByIdsAsync(advisorIds, cancellationToken);
        var items = advisorIds
            .Where(aid => map.ContainsKey(aid))
            .Select(aid => map[aid])
            .OrderBy(u => u.FullName)
            .Select(u => new { id = u.UserId, name = $"{u.FullName} ({u.Email})" })
            .ToList();

        if (items.Count == 0)
            return Json(new { items = Array.Empty<object>(), message = "Bu müdürlükte aktif danışman bulunamadı." });

        return Json(new { items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(Guid transferRequestId, bool approve, Guid? targetAdvisorUserId, string? decisionNote, CancellationToken cancellationToken)
    {
        var command = new DecideTransferCommand(transferRequestId, approve, targetAdvisorUserId, decisionNote, null);
        var result = await _transfers.DecideAsync(User.GetUserId(), command, cancellationToken);
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
                .Include(i => i.CurrentOrganizationUnit)!.ThenInclude(u => u!.Parent)
                .Where(i => !i.IsDeleted && unitIds.Contains(i.CurrentOrganizationUnitId))
                .ToListAsync(cancellationToken);

        var nameMap = await _users.GetByIdsAsync(interns.Select(i => i.UserId), cancellationToken);
        var sourceMeta = new Dictionary<string, object>();
        model.Interns = interns
            .Select(i =>
            {
                nameMap.TryGetValue(i.UserId, out var info);
                var fullName = string.IsNullOrWhiteSpace(info?.FullName) ? "(İsimsiz)" : info!.FullName;
                var branch = i.CurrentOrganizationUnit?.Name ?? "?";
                var directorate = i.CurrentOrganizationUnit?.Parent?.Name ?? "—";
                sourceMeta[i.Id.ToString()] = new { directorate, branch, fullName };
                return new SelectListItem(
                    $"{fullName} ({i.StudentNumber}) — {branch}",
                    i.Id.ToString())
                {
                    Selected = i.Id == model.InternProfileId
                };
            })
            .OrderBy(x => x.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        ViewBag.InternSourceMeta = sourceMeta;

        // Kaynak birim görünen adları
        if (model.InternProfileId != Guid.Empty)
        {
            var selected = interns.FirstOrDefault(i => i.Id == model.InternProfileId);
            if (selected is not null)
            {
                model.SourceBranchName = selected.CurrentOrganizationUnit?.Name;
                model.SourceDirectorateName = selected.CurrentOrganizationUnit?.Parent?.Name;
            }
        }

        var directorates = await _units.ListDirectoratesAsync(cancellationToken);
        model.Directorates = directorates
            .Select(d => new SelectListItem(d.Name, d.Id.ToString(), d.Id == model.TargetDirectorateId))
            .ToList();

        if (model.TargetDirectorateId != Guid.Empty)
        {
            var branches = await _units.ListBranchesByDirectorateAsync(model.TargetDirectorateId, cancellationToken);
            model.Branches = branches
                .Select(b => new SelectListItem(b.Name, b.Id.ToString(), b.Id == model.TargetOrganizationUnitId))
                .ToList();
        }
        else
        {
            model.Branches = new List<SelectListItem>();
        }

        if (model.TargetOrganizationUnitId != Guid.Empty)
        {
            var advisorIds = await _db.AdvisorUnitAssignments.AsNoTracking()
                .Where(a => a.OrganizationUnitId == model.TargetOrganizationUnitId && a.IsActive && !a.IsDeleted)
                .Select(a => a.AdvisorUserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var advisors = await _users.GetByIdsAsync(advisorIds, cancellationToken);
            model.Advisors = advisors.Values
                .OrderBy(a => a.FullName)
                .Select(a => new SelectListItem($"{a.FullName} ({a.Email})", a.UserId.ToString(), a.UserId == model.TargetAdvisorUserId))
                .ToList();
        }
        else
        {
            model.Advisors = new List<SelectListItem>();
        }

        return model;
    }

    private async Task<List<SelectListItem>> GetAllAdvisorsAsync(CancellationToken cancellationToken)
    {
        var mentors = await _accounts.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        return mentors.Select(m => new SelectListItem($"{m.FullName} ({m.Email})", m.UserId.ToString())).ToList();
    }
}
