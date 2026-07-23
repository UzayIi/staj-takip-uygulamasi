using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Feedback;
using Staj360.Domain.Enums;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class FeedbackController : Controller
{
    private readonly IInternFeedbackService _feedback;

    public FeedbackController(IInternFeedbackService feedback) => _feedback = feedback;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _feedback.ListForAdvisorAsync(User.GetUserId(), cancellationToken);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(Guid feedbackId, string replyMessage, FeedbackStatus newStatus = FeedbackStatus.Replied, CancellationToken cancellationToken = default)
    {
        var result = await _feedback.ReplyAsync(User.GetUserId(), new ReplyFeedbackCommand(feedbackId, replyMessage, newStatus), cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Cevap gönderildi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
