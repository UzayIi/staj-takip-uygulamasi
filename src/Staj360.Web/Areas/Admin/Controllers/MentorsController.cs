using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Web.Areas.Admin.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class MentorsController : Controller
{
    private readonly IUserAccountService _accountService;

    public MentorsController(IUserAccountService accountService) => _accountService = accountService;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var mentors = await _accountService.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        return View(mentors);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateStaffViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _accountService.CreateStaffAsync(new CreateStaffRequest(model.Email, model.FullName, model.Password, AppRoles.Mentor), cancellationToken);
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
    public async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _accountService.SetActiveAsync(id, isActive, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Durum güncellendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accountService.DeleteStaffAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Danışman hesabı kalıcı olarak silindi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
