using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Enums;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class DailyReportsController : Controller
{
    private const int PageSize = 20;
    private readonly IApplicationDbContext _db;

    public DailyReportsController(IApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(DailyReportStatus? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = _db.DailyReports.AsNoTracking()
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(r => !r.IsDeleted);

        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.ReportDate)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);

        ViewData["Status"] = status;
        ViewData["Page"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)PageSize);
        return View(items);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var report = await _db.DailyReports.AsNoTracking()
            .Include(r => r.WorkItems)
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);
        if (report is null) return NotFound();
        return View(report);
    }
}
