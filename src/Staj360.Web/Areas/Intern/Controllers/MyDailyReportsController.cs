using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.DailyReports;
using Staj360.Web.Areas.Intern.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyDailyReportsController : Controller
{
    private readonly IDailyReportService _service;

    public MyDailyReportsController(IDailyReportService service) => _service = service;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var reports = await _service.ListForInternAsync(User.GetUserId(), cancellationToken);
        return View(reports);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateDailyReportViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateDailyReportViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var command = new CreateDailyReportCommand(model.ReportDate, model.GeneralNotes, model.ProblemsEncountered, model.SolutionsApplied, model.TomorrowPlan);
        var result = await _service.CreateAsync(User.GetUserId(), command, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }
        TempData["Success"] = "Rapor taslağı oluşturuldu. Çalışma kalemi ekleyip danışmana gönderebilirsiniz.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetForInternAsync(User.GetUserId(), id, cancellationToken);
        if (!result.Success) return NotFound();
        return View(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> AddItem(AddDailyWorkItemViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Çalışma kalemi bilgileri geçersiz.";
            return RedirectToAction(nameof(Details), new { id = model.ReportId });
        }

        var command = new AddWorkItemCommand(model.ReportId, null, null, model.Title, model.Description, model.DurationMinutes, model.TechnologiesUsed, model.Result, model.RepositoryUrl);
        var result = await _service.AddWorkItemAsync(User.GetUserId(), command, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Çalışma kalemi eklendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = model.ReportId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveItem(Guid reportId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _service.RemoveWorkItemAsync(User.GetUserId(), reportId, itemId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Çalışma kalemi silindi." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = reportId });
    }

    [HttpPost]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SubmitAsync(User.GetUserId(), id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Rapor danışmana gönderildi." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }
}
