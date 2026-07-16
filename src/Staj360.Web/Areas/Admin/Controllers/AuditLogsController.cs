using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class AuditLogsController : Controller
{
    private const int PageSize = 30;
    private readonly IApplicationDbContext _db;

    public AuditLogsController(IApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var total = await _db.AuditLogs.CountAsync(cancellationToken);
        var items = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);

        ViewData["Page"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)PageSize);
        return View(items);
    }
}
