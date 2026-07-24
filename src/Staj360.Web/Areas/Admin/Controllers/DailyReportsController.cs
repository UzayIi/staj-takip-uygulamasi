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
    private readonly IUserDisplayLookup _users;

    public DailyReportsController(IApplicationDbContext db, IUserDisplayLookup users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index(
        DailyReportStatus? status,
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = _db.DailyReports.AsNoTracking()
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(r => !r.IsDeleted);

        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var matchedUserIds = await _users.SearchUserIdsAsync(term, cancellationToken);
            query = query.Where(r =>
                (r.InternshipPeriod!.InternProfile!.StudentNumber.Contains(term)) ||
                matchedUserIds.Contains(r.InternshipPeriod!.InternProfile!.UserId));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.ReportDate)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);

        var userIds = items
            .Select(r => r.InternshipPeriod?.InternProfile?.UserId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct();
        var names = await _users.GetByIdsAsync(userIds, cancellationToken);
        ViewBag.InternNames = names.ToDictionary(kv => kv.Key, kv => kv.Value.FullName);

        ViewData["Status"] = status;
        ViewData["Search"] = search;
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

        var userId = report.InternshipPeriod?.InternProfile?.UserId;
        if (userId.HasValue)
        {
            var names = await _users.GetByIdsAsync(new[] { userId.Value }, cancellationToken);
            ViewBag.InternFullName = names.TryGetValue(userId.Value, out var info) ? info.FullName : null;
        }

        return View(report);
    }
}
