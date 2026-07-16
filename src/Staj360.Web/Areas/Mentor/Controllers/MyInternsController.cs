using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Internships;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class MyInternsController : Controller
{
    private readonly IInternService _internService;

    public MyInternsController(IInternService internService) => _internService = internService;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var interns = await _internService.ListForMentorAsync(User.GetUserId(), cancellationToken);
        return View(interns);
    }
}
