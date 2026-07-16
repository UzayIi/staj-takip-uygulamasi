using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Projects;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyProjectsController : Controller
{
    private readonly IProjectService _projects;

    public MyProjectsController(IProjectService projects) => _projects = projects;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var projects = await _projects.ListForInternAsync(User.GetUserId(), cancellationToken);
        return View(projects);
    }
}
