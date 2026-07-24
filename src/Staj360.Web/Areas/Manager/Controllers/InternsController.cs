using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Application.Services.Exports;
using Staj360.Application.Services.Internships;
using Staj360.Web.Helpers;

namespace Staj360.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = AppPolicies.ManagerArea)]
public class InternsController : Controller
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitAssignmentService _assignments;
    private readonly IUserDisplayLookup _users;
    private readonly IInternDetailService _details;
    private readonly IInternExcelExportService _excelExport;

    public InternsController(
        IApplicationDbContext db,
        IUnitAssignmentService assignments,
        IUserDisplayLookup users,
        IInternDetailService details,
        IInternExcelExportService excelExport)
    {
        _db = db;
        _assignments = assignments;
        _users = users;
        _details = details;
        _excelExport = excelExport;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(User.GetUserId(), cancellationToken);
        if (unitIds.Count == 0)
            return View(Array.Empty<ManagerInternRow>());

        var interns = await _db.InternProfiles.AsNoTracking()
            .Include(i => i.CurrentOrganizationUnit)
            .Where(i => !i.IsDeleted && unitIds.Contains(i.CurrentOrganizationUnitId))
            .ToListAsync(cancellationToken);

        var names = await _users.GetByIdsAsync(interns.Select(i => i.UserId), cancellationToken);
        var rows = interns
            .Select(i =>
            {
                names.TryGetValue(i.UserId, out var info);
                return new ManagerInternRow(
                    i.Id,
                    string.IsNullOrWhiteSpace(info?.FullName) ? "(İsimsiz)" : info!.FullName,
                    i.StudentNumber,
                    i.CurrentOrganizationUnit?.Name ?? "—",
                    i.University,
                    i.IsActive);
            })
            .OrderBy(r => r.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return View(rows);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _details.GetForViewerAsync(
            User.GetUserId(), isAdmin: false, isManager: true, isMentor: false, id, cancellationToken);
        if (!result.Success)
            return result.ErrorCode == "FORBIDDEN" ? Forbid() : NotFound();

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _details.GetForViewerAsync(
            User.GetUserId(), isAdmin: false, isManager: true, isMentor: false, id, cancellationToken);
        if (!scope.Success)
            return scope.ErrorCode == "FORBIDDEN" ? Forbid() : NotFound();

        var export = await _excelExport.ExportAsync(id, cancellationToken);
        if (export is null)
            return NotFound();

        return File(export.Content, export.ContentType, export.FileName);
    }
}

public record ManagerInternRow(
    Guid Id,
    string FullName,
    string StudentNumber,
    string BranchName,
    string? University,
    bool IsActive);
