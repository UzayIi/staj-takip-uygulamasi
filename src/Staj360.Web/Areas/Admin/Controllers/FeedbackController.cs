using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Feedback;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class FeedbackController : Controller
{
    private readonly IInternFeedbackService _feedback;

    public FeedbackController(IInternFeedbackService feedback) => _feedback = feedback;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _feedback.ListAllForAdminAsync(cancellationToken);
        return View(items);
    }
}
