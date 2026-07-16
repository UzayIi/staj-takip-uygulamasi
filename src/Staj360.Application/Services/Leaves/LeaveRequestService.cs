using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Leaves;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;

    public LeaveRequestService(IApplicationDbContext db, IClock clock, IAuditLogService audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
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
        if (period is null)
            return ServiceResult<LeaveCreateResult>.Fail("Aktif bir staj döneminiz bulunmuyor.", "NO_ACTIVE_PERIOD");

        // Çakışan aktif izinler için uyarı (kayıt engellenmez).
        var hasOverlap = await _db.LeaveRequests.AnyAsync(l =>
            l.InternshipPeriodId == period.Id &&
            (l.Status == LeaveRequestStatus.Pending || l.Status == LeaveRequestStatus.Approved) &&
            l.StartDate <= command.EndDate && l.EndDate >= command.StartDate &&
            !l.IsDeleted, cancellationToken);

        var entity = new LeaveRequest
        {
            InternshipPeriodId = period.Id,
            LeaveType = command.LeaveType,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Reason = command.Reason.Trim(),
            DocumentPath = command.DocumentPath,
            Status = LeaveRequestStatus.Pending
        };
        _db.LeaveRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<LeaveCreateResult>.Ok(new LeaveCreateResult { Request = entity, HasOverlapWarning = hasOverlap });
    }

    public async Task<IReadOnlyList<LeaveRequest>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(l => l.InternshipPeriod!.InternProfile!.UserId == userId && !l.IsDeleted)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveRequest>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(l => l.Status == LeaveRequestStatus.Pending && !l.IsDeleted)
            .OrderBy(l => l.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<ServiceResult> ReviewAsync(Guid reviewerUserId, bool isAdmin, ReviewLeaveCommand command, CancellationToken cancellationToken = default)
    {
        var leave = await _db.LeaveRequests
            .Include(l => l.InternshipPeriod)
            .FirstOrDefaultAsync(l => l.Id == command.LeaveRequestId && !l.IsDeleted, cancellationToken);
        if (leave is null || leave.InternshipPeriod is null)
            return ServiceResult.Fail("İzin talebi bulunamadı.", "NOT_FOUND");

        // Admin tüm talepleri, Mentor yalnızca kendi stajyerinin talebini onaylayabilir.
        if (!isAdmin && leave.InternshipPeriod.MentorUserId != reviewerUserId)
            return ServiceResult.Fail("Bu izin talebini onaylama yetkiniz yok.", "FORBIDDEN");

        if (leave.Status != LeaveRequestStatus.Pending)
            return ServiceResult.Fail("Yalnızca bekleyen izin talepleri değerlendirilebilir.", "NOT_REVIEWABLE");

        leave.Status = command.Approve ? LeaveRequestStatus.Approved : LeaveRequestStatus.Rejected;
        leave.ReviewedByUserId = reviewerUserId;
        leave.ReviewedAtUtc = _clock.UtcNow;
        leave.ReviewerNote = command.ReviewerNote;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(nameof(LeaveRequest), leave.Id.ToString(), command.Approve ? "Approve" : "Reject", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }
}
