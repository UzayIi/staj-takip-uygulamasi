using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Leaves;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;
    private readonly IUnitAssignmentService _assignments;

    public LeaveRequestService(
        IApplicationDbContext db, IClock clock, IAuditLogService audit, IUnitAssignmentService assignments)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _assignments = assignments;
    }

    public async Task<ServiceResult<LeaveCreateResult>> CreateAsync(Guid userId, CreateLeaveRequestCommand command, CancellationToken cancellationToken = default)
    {
        if (command.EndDate < command.StartDate)
            return ServiceResult<LeaveCreateResult>.Fail("Başlangıç tarihi bitiş tarihinden büyük olamaz.", "VALIDATION");
        if (string.IsNullOrWhiteSpace(command.Reason))
            return ServiceResult<LeaveCreateResult>.Fail("İzin gerekçesi zorunludur.", "VALIDATION");

        var period = await _db.InternshipPeriods
            .Include(p => p.InternProfile)
            .FirstOrDefaultAsync(p => p.InternProfile!.UserId == userId && p.Status == InternshipStatus.Active && !p.IsDeleted, cancellationToken);
        if (period is null || period.InternProfile is null)
            return ServiceResult<LeaveCreateResult>.Fail("Aktif bir staj döneminiz bulunmuyor.", "NO_ACTIVE_PERIOD");

        var unitId = period.InternProfile.CurrentOrganizationUnitId;

        var hasOverlap = await _db.LeaveRequests.AnyAsync(l =>
            l.InternshipPeriodId == period.Id &&
            (l.Status == LeaveRequestStatus.Pending || l.Status == LeaveRequestStatus.Approved) &&
            l.StartDate <= command.EndDate && l.EndDate >= command.StartDate &&
            !l.IsDeleted, cancellationToken);

        var entity = new LeaveRequest
        {
            InternshipPeriodId = period.Id,
            OrganizationUnitId = unitId,
            LeaveType = command.LeaveType,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Reason = System.Net.WebUtility.HtmlEncode(command.Reason.Trim()),
            DocumentPath = command.DocumentPath,
            Status = LeaveRequestStatus.Pending
        };
        _db.LeaveRequests.Add(entity);

        // İlgili müdürlüğün yöneticilerine bildirim.
        var managers = await _assignments.GetActiveManagerUserIdsForUnitAsync(unitId, cancellationToken);
        foreach (var mid in managers)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = mid,
                Title = "Yeni izin talebi",
                Message = $"İzin: {entity.StartDate:dd.MM.yyyy} - {entity.EndDate:dd.MM.yyyy}",
                Type = NotificationType.Info
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<LeaveCreateResult>.Ok(new LeaveCreateResult { Request = entity, HasOverlapWarning = hasOverlap });
    }

    public async Task<IReadOnlyList<LeaveRequest>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Include(l => l.OrganizationUnit)
            .Where(l => l.InternshipPeriod!.InternProfile!.UserId == userId && !l.IsDeleted)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveRequest>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Include(l => l.OrganizationUnit)
            .Where(l => l.Status == LeaveRequestStatus.Pending && !l.IsDeleted)
            .OrderBy(l => l.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveRequest>> ListPendingForManagerAsync(Guid managerUserId, CancellationToken cancellationToken = default)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(managerUserId, cancellationToken);
        return await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Include(l => l.OrganizationUnit)
            .Where(l => l.Status == LeaveRequestStatus.Pending && !l.IsDeleted && unitIds.Contains(l.OrganizationUnitId))
            .OrderBy(l => l.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveRequest>> ListForMentorViewAsync(Guid mentorUserId, CancellationToken cancellationToken = default) =>
        await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Include(l => l.OrganizationUnit)
            .Where(l => !l.IsDeleted && l.InternshipPeriod!.MentorUserId == mentorUserId)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<ServiceResult> ReviewAsync(Guid reviewerUserId, ReviewLeaveCommand command, CancellationToken cancellationToken = default)
    {
        var leave = await _db.LeaveRequests
            .Include(l => l.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .FirstOrDefaultAsync(l => l.Id == command.LeaveRequestId && !l.IsDeleted, cancellationToken);
        if (leave is null || leave.InternshipPeriod is null)
            return ServiceResult.Fail("İzin talebi bulunamadı.", "NOT_FOUND");

        // Yalnızca ilgili müdürlüğün aktif yöneticileri onaylayabilir/reddedebilir.
        if (!await _assignments.IsManagerOfUnitAsync(reviewerUserId, leave.OrganizationUnitId, cancellationToken))
            return ServiceResult.Fail("Bu izin talebini yalnızca ilgili müdürlüğün yöneticileri sonuçlandırabilir.", "FORBIDDEN");

        if (leave.Status != LeaveRequestStatus.Pending)
            return ServiceResult.Fail("Yalnızca bekleyen izin talepleri değerlendirilebilir.", "NOT_REVIEWABLE");

        if (command.RowVersion is not null)
            leave.RowVersion = command.RowVersion;

        leave.Status = command.Approve ? LeaveRequestStatus.Approved : LeaveRequestStatus.Rejected;
        leave.ReviewedByUserId = reviewerUserId;
        leave.ReviewedAtUtc = _clock.UtcNow;
        leave.ReviewerNote = command.ReviewerNote is null ? null : System.Net.WebUtility.HtmlEncode(command.ReviewerNote);

        var internUserId = leave.InternshipPeriod.InternProfile?.UserId;
        var mentorUserId = leave.InternshipPeriod.MentorUserId;
        var statusText = command.Approve ? "onaylandı" : "reddedildi";

        if (internUserId.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = internUserId.Value,
                Title = $"İzin talebiniz {statusText}",
                Message = $"{leave.StartDate:dd.MM.yyyy} - {leave.EndDate:dd.MM.yyyy}",
                Type = command.Approve ? NotificationType.Success : NotificationType.Warning
            });
        }

        _db.Notifications.Add(new Notification
        {
            UserId = mentorUserId,
            Title = $"Stajyer izin talebi {statusText}",
            Message = $"{leave.StartDate:dd.MM.yyyy} - {leave.EndDate:dd.MM.yyyy}",
            Type = NotificationType.Info
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("İzin talebi başka bir yönetici tarafından sonuçlandırılmış.", "CONCURRENCY");
        }

        await _audit.LogAsync(nameof(LeaveRequest), leave.Id.ToString(), command.Approve ? "Approve" : "Reject", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }
}
