using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Ai;
using Staj360.Application.Common;
using Staj360.Web.Helpers;
using Staj360.Web.Models;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class AiSummariesController : Controller
{
    private readonly IReportSummaryService _summaryService;
    private readonly IApplicationDbContext _db;

    public AiSummariesController(IReportSummaryService summaryService, IApplicationDbContext db)
    {
        _summaryService = summaryService;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var mentorId = User.GetUserId();
        var summaries = await _db.AiReportSummaries.AsNoTracking()
            .Include(s => s.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(s => s.InternshipPeriod != null && s.InternshipPeriod.MentorUserId == mentorId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        ViewData["AiEnabled"] = _summaryService.IsEnabled;
        return View(summaries);
    }

    [HttpGet]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        var vm = new GenerateAiSummaryViewModel { AiEnabled = _summaryService.IsEnabled };
        vm.Periods = await GetPeriodsAsync(cancellationToken);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Generate(GenerateAiSummaryViewModel model, CancellationToken cancellationToken)
    {
        var mentorId = User.GetUserId();
        if (!_summaryService.IsEnabled)
        {
            TempData["Warning"] = "Yapay zekâ özeti devre dışı.";
            return RedirectToAction(nameof(Index));
        }

        // Mentor yalnızca kendi stajyerinin dönemine özet üretebilir.
        var owns = await _db.InternshipPeriods.AsNoTracking()
            .AnyAsync(p => p.Id == model.InternshipPeriodId && p.MentorUserId == mentorId, cancellationToken);
        if (!owns)
        {
            TempData["Error"] = "Bu staj dönemine erişim yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            model.AiEnabled = true;
            model.Periods = await GetPeriodsAsync(cancellationToken);
            return View(model);
        }

        var command = new GenerateSummaryCommand(model.InternshipPeriodId, model.PeriodStart, model.PeriodEnd, model.SummaryType, mentorId);
        var result = await _summaryService.GenerateAsync(command, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            model.AiEnabled = true;
            model.Periods = await GetPeriodsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Yapay zekâ özeti oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var mentorId = User.GetUserId();
        var summary = await _db.AiReportSummaries.AsNoTracking()
            .Include(s => s.InternshipPeriod)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (summary is null) return NotFound();
        if (summary.InternshipPeriod?.MentorUserId != mentorId) return Forbid();
        return View(AiReportSummaryViewModel.From(summary));
    }

    private async Task<List<SelectListItem>> GetPeriodsAsync(CancellationToken cancellationToken)
    {
        var mentorId = User.GetUserId();
        var periods = await _db.InternshipPeriods.AsNoTracking()
            .Include(p => p.InternProfile)
            .Where(p => !p.IsDeleted && p.MentorUserId == mentorId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        return periods.Select(p => new SelectListItem(
            $"{p.InternProfile?.StudentNumber} ({p.StartDate:dd.MM.yyyy} - {p.EndDate:dd.MM.yyyy})",
            p.Id.ToString())).ToList();
    }
}
