using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Common;
using Staj360.Application.Services.Evaluations;
using Staj360.Application.Services.Internships;
using Staj360.Web.Areas.Mentor.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class EvaluationsController : Controller
{
    private readonly IEvaluationService _evaluations;
    private readonly IInternshipPeriodService _periods;

    public EvaluationsController(IEvaluationService evaluations, IInternshipPeriodService periods)
    {
        _evaluations = evaluations;
        _periods = periods;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _evaluations.ListForMentorAsync(User.GetUserId(), cancellationToken);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new CreateEvaluationViewModel { Periods = await GetPeriodsAsync(cancellationToken) };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateEvaluationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Periods = await GetPeriodsAsync(cancellationToken);
            return View(model);
        }

        var command = new CreateEvaluationCommand(
            model.InternshipPeriodId, model.EvaluationDate,
            model.TechnicalKnowledgeScore, model.ResponsibilityScore, model.TeamworkScore,
            model.CommunicationScore, model.ProblemSolvingScore, model.TimeManagementScore,
            model.AttendanceScore, model.GeneralComment);

        var result = await _evaluations.CreateAsync(User.GetUserId(), command, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            model.Periods = await GetPeriodsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Değerlendirme kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetPeriodsAsync(CancellationToken cancellationToken)
    {
        var periods = await _periods.ListForMentorAsync(User.GetUserId(), cancellationToken);
        return periods.Select(p => new SelectListItem(
            $"{p.InternProfile?.StudentNumber} ({p.StartDate:dd.MM.yyyy} - {p.EndDate:dd.MM.yyyy})",
            p.Id.ToString())).ToList();
    }
}
