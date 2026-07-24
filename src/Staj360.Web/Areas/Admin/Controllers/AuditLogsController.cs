using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Web.Areas.Admin.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class AuditLogsController : Controller
{
    private const int PageSize = 30;
    private readonly IApplicationDbContext _db;

    public AuditLogsController(IApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(AuditLogFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        filter ??= new AuditLogFilterViewModel();
        if (filter.Page < 1) filter.Page = 1;

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(a => a.ActorUserId == filter.UserId || a.UserId == filter.UserId);

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var role = filter.Role.Trim();
            query = query.Where(a => a.ActorRoleSnapshot != null && a.ActorRoleSnapshot == role);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim();
            query = query.Where(a => a.Action.Contains(action));
        }

        if (filter.OrganizationUnitId.HasValue)
            query = query.Where(a => a.OrganizationUnitId == filter.OrganizationUnitId);

        if (filter.FromUtc.HasValue)
            query = query.Where(a => a.CreatedAtUtc >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(a => a.CreatedAtUtc <= filter.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(filter.IpAddress))
        {
            var ip = filter.IpAddress.Trim();
            query = query.Where(a => a.IpAddress != null && a.IpAddress.Contains(ip));
        }

        if (filter.IsSuccessful.HasValue)
            query = query.Where(a => a.IsSuccessful == filter.IsSuccessful.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(a =>
                (a.SafeDescription != null && a.SafeDescription.Contains(s))
                || (a.ActorNameSnapshot != null && a.ActorNameSnapshot.Contains(s))
                || a.EntityName.Contains(s)
                || a.EntityId.Contains(s)
                || a.Action.Contains(s));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((filter.Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        filter.TotalCount = total;
        filter.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        filter.Items = items;

        var units = await _db.OrganizationUnits.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.DisplayOrder).ThenBy(u => u.Name)
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(cancellationToken);
        ViewBag.Units = units;

        return View(filter);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (item is null)
            return NotFound();
        return View(item);
    }
}
