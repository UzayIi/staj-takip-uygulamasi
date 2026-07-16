using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Projects;
using Staj360.Domain.Enums;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyTasksController : Controller
{
    private readonly IProjectTaskService _tasks;

    public MyTasksController(IProjectTaskService tasks) => _tasks = tasks;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tasks = await _tasks.ListForInternAsync(User.GetUserId(), cancellationToken);
        return View(tasks);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(Guid taskId, ProjectTaskStatus status, CancellationToken cancellationToken)
    {
        var result = await _tasks.UpdateStatusByInternAsync(User.GetUserId(), taskId, status, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Görev durumu güncellendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
