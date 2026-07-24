using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;

namespace Staj360.Web.Areas.Mentor.Controllers;

[Area("Mentor")]
[Authorize(Policy = AppPolicies.MentorArea)]
public class TransfersController : Controller
{
    [HttpGet]
    public IActionResult Create() => Forbid();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(object _) => Forbid();
}
