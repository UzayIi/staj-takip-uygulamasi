using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Enums;
using Staj360.Web.Areas.Admin.Models;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class DashboardController : Controller
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ITimeZoneService _tz;

    public DashboardController(IApplicationDbContext db, IClock clock, ITimeZoneService tz)
    {
        _db = db;
        _clock = clock;
        _tz = tz;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = _tz.LocalDate(_clock.UtcNow);
        var soon = today.AddDays(7);

        var vm = new AdminDashboardViewModel
        {
            ActiveInterns = await _db.InternProfiles.CountAsync(i => i.IsActive && !i.IsDeleted, cancellationToken),
            CheckedInToday = await _db.AttendanceDays.CountAsync(a => a.WorkDate == today && a.FirstCheckInUtc != null && !a.IsDeleted, cancellationToken),
            LateToday = await _db.AttendanceDays.CountAsync(a => a.WorkDate == today && a.IsLate && !a.IsDeleted, cancellationToken),
            IncompleteToday = await _db.AttendanceDays.CountAsync(a => a.WorkDate == today && a.IsIncomplete && !a.IsDeleted, cancellationToken),
            OnLeaveToday = await _db.LeaveRequests.CountAsync(l => l.Status == LeaveRequestStatus.Approved && l.StartDate <= today && l.EndDate >= today && !l.IsDeleted, cancellationToken),
            PendingReports = await _db.DailyReports.CountAsync(r => r.Status == DailyReportStatus.Submitted && !r.IsDeleted, cancellationToken),
            PendingLeaves = await _db.LeaveRequests.CountAsync(l => l.Status == LeaveRequestStatus.Pending && !l.IsDeleted, cancellationToken),
            OngoingProjects = await _db.Projects.CountAsync(p => p.Status == ProjectStatus.InProgress && !p.IsDeleted, cancellationToken),
            EndingSoon = await _db.InternshipPeriods.CountAsync(p => p.Status == InternshipStatus.Active && p.EndDate >= today && p.EndDate <= soon && !p.IsDeleted, cancellationToken)
        };

        vm.RecentAudits = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(10)
            .Select(a => new AuditRow(a.EntityName, a.Action, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return View(vm);
    }
}
