using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Web.Helpers;
using Staj360.Web.Models;

namespace Staj360.Web.Areas.Intern.Controllers;

[Area("Intern")]
[Authorize(Policy = AppPolicies.InternArea)]
public class MySummariesController : Controller
{
    private readonly IApplicationDbContext _db;

    public MySummariesController(IApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var summaries = await _db.AiReportSummaries.AsNoTracking()
            .Include(s => s.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(s => s.InternshipPeriod != null && s.InternshipPeriod.InternProfile != null
                        && s.InternshipPeriod.InternProfile.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return View(summaries);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var summary = await _db.AiReportSummaries.AsNoTracking()
            .Include(s => s.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (summary is null) return NotFound();
        if (summary.InternshipPeriod?.InternProfile?.UserId != userId) return Forbid();
        return View(AiReportSummaryViewModel.From(summary));
    }
}
