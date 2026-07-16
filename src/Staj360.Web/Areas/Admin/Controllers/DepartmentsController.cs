using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Departments;
using Staj360.Web.Areas.Admin.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class DepartmentsController : Controller
{
    private readonly IDepartmentService _service;

    public DepartmentsController(IDepartmentService service) => _service = service;

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _service.ListAsync(search, page, 20, cancellationToken);
        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    public IActionResult Create() => View(new DepartmentViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(DepartmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _service.CreateAsync(new DepartmentInput(model.Name, model.Description, model.IsActive), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }
        TempData["Success"] = "Departman oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var d = await _service.GetAsync(id, cancellationToken);
        if (d is null) return NotFound();
        return View(new DepartmentViewModel { Id = d.Id, Name = d.Name, Description = d.Description, IsActive = d.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(DepartmentViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var result = await _service.UpdateAsync(model.Id.Value, new DepartmentInput(model.Name, model.Description, model.IsActive), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }
        TempData["Success"] = "Departman güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Departman silindi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
