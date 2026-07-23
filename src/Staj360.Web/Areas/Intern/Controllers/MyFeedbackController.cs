using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Feedback;
using Staj360.Web.Areas.Intern.Models;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyFeedbackController : Controller
{
    private readonly IInternFeedbackService _feedback;
    private readonly IUserDisplayLookup _users;

    public MyFeedbackController(IInternFeedbackService feedback, IUserDisplayLookup users)
    {
        _feedback = feedback;
        _users = users;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _feedback.ListForInternAsync(User.GetUserId(), cancellationToken);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildVmAsync(new CreateFeedbackViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateFeedbackViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(await BuildVmAsync(model, cancellationToken));

        var result = await _feedback.CreateAsync(
            User.GetUserId(),
            new CreateFeedbackCommand(model.AdvisorUserId, model.Title, model.Message),
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Gönderilemedi.");
            return View(await BuildVmAsync(model, cancellationToken));
        }

        TempData["Success"] = "Geri bildiriminiz gönderildi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CreateFeedbackViewModel> BuildVmAsync(CreateFeedbackViewModel model, CancellationToken cancellationToken)
    {
        var advisorIds = await _feedback.GetAllowedAdvisorIdsAsync(User.GetUserId(), cancellationToken);
        var map = await _users.GetByIdsAsync(advisorIds, cancellationToken);
        model.Advisors = advisorIds
            .Select(id =>
            {
                map.TryGetValue(id, out var info);
                return new SelectListItem(info?.FullName ?? id.ToString(), id.ToString());
            })
            .ToList();
        return model;
    }
}
