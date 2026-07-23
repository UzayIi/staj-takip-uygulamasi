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
public class MentorsController : Controller
{
    private readonly IUserAccountService _accountService;
    private readonly IUnitAssignmentService _assignments;
    private readonly IOrganizationUnitService _units;

    public MentorsController(
        IUserAccountService accountService,
        IUnitAssignmentService assignments,
        IOrganizationUnitService units)
    {
        _accountService = accountService;
        _assignments = assignments;
        _units = units;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var mentors = await _accountService.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        return View(mentors);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateStaffViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStaffViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _accountService.CreateStaffAsync(
            new CreateStaffRequest(model.Email, model.FullName, model.Password, AppRoles.Mentor), cancellationToken);
        if (!result.Success)
        {
            foreach (var err in result.ValidationErrors) ModelState.AddModelError(string.Empty, err);
            if (result.ValidationErrors.Count == 0 && result.ErrorMessage is not null)
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            return View(model);
        }
        TempData["Success"] = "Danışman hesabı oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _accountService.SetActiveAsync(id, isActive, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Durum güncellendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accountService.DeleteStaffAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Danışman hesabı kalıcı olarak silindi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AssignUnits(Guid id, CancellationToken cancellationToken)
    {
        var mentor = await _accountService.GetAsync(id, cancellationToken);
        if (mentor is null) return NotFound();

        var assigned = await _assignments.GetAdvisorUnitIdsAsync(id, cancellationToken);
        var branches = await _units.ListBranchesAsync(cancellationToken);

        var vm = new AssignUnitsViewModel
        {
            UserId = id,
            FullName = mentor.FullName,
            Email = mentor.Email,
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
        var result = await _assignments.AssignAdvisorAsync(userId, organizationUnitId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Birim atandı." : result.ErrorMessage;
        return RedirectToAction(nameof(AssignUnits), new { id = userId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignUnit(Guid userId, Guid organizationUnitId, CancellationToken cancellationToken)
    {
        var result = await _assignments.UnassignAdvisorAsync(userId, organizationUnitId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Birim ataması kaldırıldı." : result.ErrorMessage;
        return RedirectToAction(nameof(AssignUnits), new { id = userId });
    }
}
