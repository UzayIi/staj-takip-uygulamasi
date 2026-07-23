using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Internships;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MyAttendanceController : Controller
{
    private readonly IApplicationDbContext _db;
    private readonly IInternshipPeriodService _periods;

    public MyAttendanceController(IApplicationDbContext db, IInternshipPeriodService periods)
    {
        _db = db;
        _periods = periods;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var period = await _periods.GetActiveForUserAsync(User.GetUserId(), cancellationToken);
        if (period is null)
        {
            return View(Array.Empty<Staj360.Domain.Entities.AttendanceDay>());
        }

        var days = await _db.AttendanceDays.AsNoTracking()
            .Where(d => d.InternshipPeriodId == period.Id && !d.IsDeleted)
            .OrderByDescending(d => d.WorkDate)
            .Take(90)
            .ToListAsync(cancellationToken);

        return View(days);
    }
}
