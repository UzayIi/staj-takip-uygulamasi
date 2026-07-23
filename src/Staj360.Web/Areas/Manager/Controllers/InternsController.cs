using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = AppPolicies.ManagerArea)]
public class InternsController : Controller
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitAssignmentService _assignments;

    public InternsController(IApplicationDbContext db, IUnitAssignmentService assignments)
    {
        _db = db;
        _assignments = assignments;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        if (unitIds.Count == 0)
            return View(Array.Empty<Staj360.Domain.Entities.InternProfile>());

        var interns = await _db.InternProfiles.AsNoTracking()
            .Include(i => i.CurrentOrganizationUnit)
            .Where(i => !i.IsDeleted && unitIds.Contains(i.CurrentOrganizationUnitId))
            .OrderBy(i => i.StudentNumber)
            .ToListAsync(cancellationToken);

        return View(interns);
    }
}
