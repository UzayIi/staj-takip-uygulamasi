using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Departments;
using Staj360.Application.Services.Internships;
using Staj360.Web.Areas.Admin.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class InternsController : Controller
{
    private readonly IInternService _internService;
    private readonly IDepartmentService _departmentService;
    private readonly IUserAccountService _accountService;

    public InternsController(IInternService internService, IDepartmentService departmentService, IUserAccountService accountService)
    {
        _internService = internService;
        _departmentService = departmentService;
        _accountService = accountService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _internService.ListAsync(search, page, 20, cancellationToken);
        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new CreateInternViewModel { Departments = await GetDepartmentsAsync(cancellationToken) };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInternViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Departments = await GetDepartmentsAsync(cancellationToken);
            return View(model);
        }

        var request = new CreateInternRequest(
            model.Email, model.FullName, model.Password, model.StudentNumber, model.DepartmentId,
            model.NationalId, model.University, model.Faculty, model.SchoolDepartment, model.ClassLevel,
            model.PhoneNumber, model.EmergencyContactName, model.EmergencyContactPhone);

        var result = await _accountService.CreateInternAsync(request, cancellationToken);
        if (!result.Success)
        {
            foreach (var err in result.ValidationErrors) ModelState.AddModelError(string.Empty, err);
            if (result.ValidationErrors.Count == 0 && result.ErrorMessage is not null)
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            model.Departments = await GetDepartmentsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Stajyer hesabı ve profili oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var departments = await _departmentService.ListActiveAsync(cancellationToken);
        return departments.Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToList();
    }
}
