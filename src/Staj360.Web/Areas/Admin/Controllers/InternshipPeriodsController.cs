using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Internships;
using Staj360.Web.Areas.Admin.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class InternshipPeriodsController : Controller
{
    private readonly IInternshipPeriodService _periodService;
    private readonly IInternService _internService;
    private readonly IWorkScheduleService _scheduleService;
    private readonly IUserAccountService _accountService;

    public InternshipPeriodsController(
        IInternshipPeriodService periodService,
        IInternService internService,
        IWorkScheduleService scheduleService,
        IUserAccountService accountService)
    {
        _periodService = periodService;
        _internService = internService;
        _scheduleService = scheduleService;
        _accountService = accountService;
    }

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _periodService.ListAsync(page, 20, cancellationToken);
        
        var internsResult = await _internService.ListAsync(null, null, 1, 1000, cancellationToken);
        var internNames = internsResult.Items.ToDictionary(i => i.InternProfileId, i => i.FullName);
        ViewBag.InternNames = internNames;
        
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new CreateInternshipPeriodViewModel();
        await FillListsAsync(vm, cancellationToken);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInternshipPeriodViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await FillListsAsync(model, cancellationToken);
            return View(model);
        }

        var command = new CreateInternshipPeriodCommand(
            model.InternProfileId, model.MentorUserId, model.WorkScheduleId,
            model.StartDate, model.EndDate, model.RequiredWorkDays);

        var result = await _periodService.CreateAsync(command, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await FillListsAsync(model, cancellationToken);
            return View(model);
        }
        TempData["Success"] = "Staj dönemi oluşturuldu. Etkinleştirmek için 'Aktifleştir' butonunu kullanın.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _periodService.ActivateAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Staj dönemi aktifleştirildi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _periodService.CompleteAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Staj dönemi tamamlandı olarak işaretlendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync(CreateInternshipPeriodViewModel vm, CancellationToken cancellationToken)
    {
        var interns = await _internService.ListAsync(null, null, 1, 200, cancellationToken);
        vm.Interns = interns.Items
            .Select(i => new SelectListItem($"{i.FullName} ({i.StudentNumber}) - {i.DepartmentName}", i.InternProfileId.ToString()))
            .ToList();

        var mentors = await _accountService.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        vm.Mentors = mentors.Select(m => new SelectListItem(m.FullName, m.UserId.ToString())).ToList();

        var schedules = await _scheduleService.ListAsync(cancellationToken);
        vm.Schedules = schedules.Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList();
    }
}
