using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Organization;
using Staj360.Web.Areas.Admin.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class ManagersController : Controller
{
    private readonly IUserAccountService _accounts;
    private readonly IUnitAssignmentService _assignments;
    private readonly IOrganizationUnitService _units;

    public ManagersController(
        IUserAccountService accounts,
        IUnitAssignmentService assignments,
        IOrganizationUnitService units)
    {
        _accounts = accounts;
        _assignments = assignments;
        _units = units;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var managers = await _accounts.ListByRoleAsync(AppRoles.Manager, cancellationToken);
        return View(managers);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateStaffViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStaffViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _accounts.CreateStaffAsync(
            new CreateStaffRequest(model.Email, model.FullName, model.Password, AppRoles.Manager), cancellationToken);
        if (!result.Success)
        {
            foreach (var err in result.ValidationErrors) ModelState.AddModelError(string.Empty, err);
            if (result.ValidationErrors.Count == 0 && result.ErrorMessage is not null)
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            return View(model);
        }

        TempData["Success"] = "Yönetici hesabı oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _accounts.SetActiveAsync(id, isActive, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Durum güncellendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AssignUnits(Guid id, CancellationToken cancellationToken)
    {
        var manager = await _accounts.GetAsync(id, cancellationToken);
        if (manager is null) return NotFound();

        var assigned = await _assignments.GetManagerUnitIdsAsync(id, cancellationToken);
        var branches = await _units.ListBranchesAsync(cancellationToken);

        var vm = new AssignUnitsViewModel
        {
            UserId = id,
            FullName = manager.FullName,
            Email = manager.Email,
            AssignedUnitIds = assigned.ToHashSet(),
            Branches = branches.Select(b => new SelectListItem(
                b.ParentName is null ? b.Name : $"{b.ParentName} / {b.Name}",
                b.Id.ToString())).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUnit(Guid userId, Guid organizationUnitId, CancellationToken cancellationToken)
    {
        var result = await _assignments.AssignManagerAsync(userId, organizationUnitId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Birim atandı." : result.ErrorMessage;
        return RedirectToAction(nameof(AssignUnits), new { id = userId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignUnit(Guid userId, Guid organizationUnitId, CancellationToken cancellationToken)
    {
        var result = await _assignments.UnassignManagerAsync(userId, organizationUnitId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Birim ataması kaldırıldı." : result.ErrorMessage;
        return RedirectToAction(nameof(AssignUnits), new { id = userId });
    }
}
