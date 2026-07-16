using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Internships;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyProfileController : Controller
{
    private readonly IInternService _interns;

    public MyProfileController(IInternService interns) => _interns = interns;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await _interns.GetByUserIdAsync(User.GetUserId(), cancellationToken);
        if (profile is null) return NotFound();
        return View(profile);
    }
}
