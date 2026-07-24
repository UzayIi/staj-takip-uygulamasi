using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Exports;
using Staj360.Application.Services.Internships;
using Staj360.Application.Services.Organization;
using Staj360.Web.Areas.Admin.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class InternsController : Controller
{
    private readonly IInternService _internService;
    private readonly IOrganizationUnitService _organizationUnitService;
    private readonly IUserAccountService _accountService;
    private readonly IInternExcelExportService _excelExport;
    private readonly IInternDetailService _details;
    private readonly IApplicationDbContext _db;

    public InternsController(
        IInternService internService,
        IOrganizationUnitService organizationUnitService,
        IUserAccountService accountService,
        IInternExcelExportService excelExport,
        IInternDetailService details,
        IApplicationDbContext db)
    {
        _internService = internService;
        _organizationUnitService = organizationUnitService;
        _accountService = accountService;
        _excelExport = excelExport;
        _details = details;
        _db = db;
    }

    public async Task<IActionResult> Index(
        string? search,
        int? year,
        DateOnly? startDate,
        DateOnly? endDate,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
            ModelState.AddModelError(string.Empty, "Başlangıç tarihi bitiş tarihinden büyük olamaz.");

        var effectiveStart = ModelState.IsValid ? startDate : null;
        var effectiveEnd = ModelState.IsValid ? endDate : null;

        var result = await _internService.ListAsync(search, year, effectiveStart, effectiveEnd, page, 20, cancellationToken);
        ViewData["Search"] = search;
        ViewData["Year"] = year;
        ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
        ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");

        var years = await _db.InternshipPeriods.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => p.StartDate.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(cancellationToken);
        ViewData["Years"] = years;

        return View(result);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _details.GetForViewerAsync(
            User.GetUserId(), isAdmin: true, isManager: false, isMentor: false, id, cancellationToken);
        if (!result.Success)
            return NotFound();
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _details.GetForViewerAsync(
            User.GetUserId(), isAdmin: true, isManager: false, isMentor: false, id, cancellationToken);
        if (!scope.Success)
            return NotFound();

        var export = await _excelExport.ExportAsync(id, cancellationToken);
        if (export is null)
            return NotFound();

        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new CreateInternViewModel();
        await FillListsAsync(vm, cancellationToken);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateInternViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await FillListsAsync(model, cancellationToken);
            model.Confirmed = false;
            return View(model);
        }

        if (!model.Confirmed)
        {
            await FillListsAsync(model, cancellationToken);
            ViewData["ShowCreateConfirm"] = true;
            return View(model);
        }

        var request = new CreateInternRequest(
            model.Email, model.FullName, model.Password, model.StudentNumber,
            model.OrganizationUnitId, model.AdvisorUserId,
            model.NationalId, model.University, model.Faculty, model.SchoolDepartment, model.ClassLevel,
            model.PhoneNumber, model.Address, model.EmergencyContactName, model.EmergencyContactPhone);

        var result = await _accountService.CreateInternAsync(request, cancellationToken);
        if (!result.Success)
        {
            foreach (var err in result.ValidationErrors) ModelState.AddModelError(string.Empty, err);
            if (result.ValidationErrors.Count == 0 && result.ErrorMessage is not null)
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            await FillListsAsync(model, cancellationToken);
            model.Confirmed = false;
            return View(model);
        }

        TempData["Success"] = "Stajyer başarıyla eklendi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync(CreateInternViewModel model, CancellationToken cancellationToken)
    {
        var branches = await _organizationUnitService.ListBranchesAsync(cancellationToken);
        model.OrganizationUnits = branches
            .Select(d => new SelectListItem(d.ParentName is null ? d.Name : $"{d.ParentName} / {d.Name}", d.Id.ToString()))
            .ToList();

        var mentors = await _accountService.ListByRoleAsync(AppRoles.Mentor, cancellationToken);
        model.Advisors = mentors
            .Select(m => new SelectListItem($"{m.FullName} ({m.Email})", m.UserId.ToString()))
            .ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accountService.DeleteInternAsync(id, cancellationToken);
        if (result.Success)
            TempData["Success"] = result.SuccessMessage ?? "Stajyer başarıyla silindi.";
        else
            TempData["Error"] = result.ErrorMessage ?? "Stajyer silinemedi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Delete() => BadRequest("Silme işlemi yalnızca POST ile yapılabilir.");
}
