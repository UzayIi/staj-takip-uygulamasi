using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.DailyReports;

/// <summary>
/// Günlük rapor akışını (Draft → Submitted → RevisionRequested/Approved/Rejected)
/// ve yetkilendirme kurallarını uygular.
/// </summary>
public class DailyReportService : IDailyReportService
{
    // Bir çalışma kaleminin makul üst süre sınırı (dakika). Mantıksız yüksek değerler reddedilir.
    private const int MaxWorkItemMinutes = 24 * 60;

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;

    public DailyReportService(IApplicationDbContext db, IClock clock, IAuditLogService audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<ServiceResult<DailyReport>> CreateAsync(Guid userId, CreateDailyReportCommand command, CancellationToken cancellationToken = default)
    {
        var period = await GetActivePeriodAsync(userId, cancellationToken);
        if (period is null)
            return ServiceResult<DailyReport>.Fail("Aktif bir staj döneminiz bulunmuyor.", "NO_ACTIVE_PERIOD");

        var exists = await _db.DailyReports.AnyAsync(r =>
            r.InternshipPeriodId == period.Id && r.ReportDate == command.ReportDate && !r.IsDeleted, cancellationToken);
        if (exists)
            return ServiceResult<DailyReport>.Fail("Bu tarih için zaten bir raporunuz var. Aynı gün ikinci rapor oluşturulamaz.", "DUPLICATE_REPORT");

        var report = new DailyReport
        {
            InternshipPeriodId = period.Id,
            ReportDate = command.ReportDate,
            GeneralNotes = command.GeneralNotes,
            ProblemsEncountered = command.ProblemsEncountered,
            SolutionsApplied = command.SolutionsApplied,
            TomorrowPlan = command.TomorrowPlan,
            Status = DailyReportStatus.Draft
        };
        _db.DailyReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<DailyReport>.Ok(report);
    }

    public async Task<ServiceResult> UpdateAsync(Guid userId, UpdateDailyReportCommand command, CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(userId, command.ReportId, cancellationToken);
        if (report is null)
            return ServiceResult.Fail("Rapor bulunamadı veya erişim yetkiniz yok.", "NOT_FOUND");

        if (!IsEditable(report.Status))
            return ServiceResult.Fail("Gönderilmiş veya onaylanmış rapor düzenlenemez.", "NOT_EDITABLE");

        report.GeneralNotes = command.GeneralNotes;
        report.ProblemsEncountered = command.ProblemsEncountered;
        report.SolutionsApplied = command.SolutionsApplied;
        report.TomorrowPlan = command.TomorrowPlan;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<DailyWorkItem>> AddWorkItemAsync(Guid userId, AddWorkItemCommand command, CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(userId, command.ReportId, cancellationToken);
        if (report is null)
            return ServiceResult<DailyWorkItem>.Fail("Rapor bulunamadı veya erişim yetkiniz yok.", "NOT_FOUND");

        if (!IsEditable(report.Status))
            return ServiceResult<DailyWorkItem>.Fail("Bu durumdaki rapora çalışma kalemi eklenemez.", "NOT_EDITABLE");

        if (string.IsNullOrWhiteSpace(command.Title))
            return ServiceResult<DailyWorkItem>.Fail("Çalışma başlığı zorunludur.", "VALIDATION");

        if (command.DurationMinutes <= 0)
            return ServiceResult<DailyWorkItem>.Fail("Süre pozitif bir değer olmalıdır.", "VALIDATION");

        if (command.DurationMinutes > MaxWorkItemMinutes)
            return ServiceResult<DailyWorkItem>.Fail("Süre bir gün için makul değerin üzerinde.", "VALIDATION");

        var item = new DailyWorkItem
        {
            DailyReportId = report.Id,
            ProjectId = command.ProjectId,
            ProjectTaskId = command.ProjectTaskId,
            Title = command.Title.Trim(),
            Description = command.Description,
            DurationMinutes = command.DurationMinutes,
            TechnologiesUsed = command.TechnologiesUsed,
            Result = command.Result,
            RepositoryUrl = command.RepositoryUrl
        };
        _db.DailyWorkItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<DailyWorkItem>.Ok(item);
    }

    public async Task<ServiceResult> RemoveWorkItemAsync(Guid userId, Guid reportId, Guid workItemId, CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(userId, reportId, cancellationToken);
        if (report is null)
            return ServiceResult.Fail("Rapor bulunamadı veya erişim yetkiniz yok.", "NOT_FOUND");

        if (!IsEditable(report.Status))
            return ServiceResult.Fail("Bu durumdaki rapordan çalışma kalemi çıkarılamaz.", "NOT_EDITABLE");

        var item = report.WorkItems.FirstOrDefault(w => w.Id == workItemId);
        if (item is null)
            return ServiceResult.Fail("Çalışma kalemi bulunamadı.", "NOT_FOUND");

        _db.DailyWorkItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SubmitAsync(Guid userId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(userId, reportId, cancellationToken);
        if (report is null)
            return ServiceResult.Fail("Rapor bulunamadı veya erişim yetkiniz yok.", "NOT_FOUND");

        if (!IsEditable(report.Status))
            return ServiceResult.Fail("Yalnızca taslak veya düzeltme istenen rapor gönderilebilir.", "NOT_SUBMITTABLE");

        if (!report.WorkItems.Any())
            return ServiceResult.Fail("Raporda en az bir çalışma kalemi bulunmadan gönderilemez.", "NO_WORK_ITEM");

        report.Status = DailyReportStatus.Submitted;
        report.SubmittedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(DailyReport), report.Id.ToString(), "Submit", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReviewAsync(Guid mentorUserId, ReviewDailyReportCommand command, CancellationToken cancellationToken = default)
    {
        var report = await _db.DailyReports
            .Include(r => r.WorkItems)
            .Include(r => r.InternshipPeriod)
            .FirstOrDefaultAsync(r => r.Id == command.ReportId && !r.IsDeleted, cancellationToken);

        if (report is null || report.InternshipPeriod is null)
            return ServiceResult.Fail("Rapor bulunamadı.", "NOT_FOUND");

        // Mentor yalnızca kendisine atanmış stajyerin raporunu inceleyebilir.
        if (report.InternshipPeriod.MentorUserId != mentorUserId)
            return ServiceResult.Fail("Bu rapor size atanmış bir stajyere ait değil.", "FORBIDDEN");

        if (report.Status != DailyReportStatus.Submitted)
            return ServiceResult.Fail("Yalnızca gönderilmiş raporlar incelenebilir.", "NOT_REVIEWABLE");

        if (command.Decision is ReviewDecision.RequestRevision or ReviewDecision.Reject
            && string.IsNullOrWhiteSpace(command.MentorComment))
            return ServiceResult.Fail("Düzeltme veya red işleminde açıklama girmek zorunludur.", "COMMENT_REQUIRED");

        report.Status = command.Decision switch
        {
            ReviewDecision.Approve => DailyReportStatus.Approved,
            ReviewDecision.RequestRevision => DailyReportStatus.RevisionRequested,
            ReviewDecision.Reject => DailyReportStatus.Rejected,
            _ => report.Status
        };
        report.MentorComment = command.MentorComment;
        report.ReviewedAtUtc = _clock.UtcNow;
        report.ReviewedByUserId = mentorUserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(nameof(DailyReport), report.Id.ToString(), $"Review:{command.Decision}", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<DailyReport>> GetForInternAsync(Guid userId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await _db.DailyReports
            .AsNoTracking()
            .Include(r => r.WorkItems)
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .FirstOrDefaultAsync(r => r.Id == reportId && !r.IsDeleted, cancellationToken);

        if (report is null || report.InternshipPeriod?.InternProfile?.UserId != userId)
            return ServiceResult<DailyReport>.Fail("Rapor bulunamadı veya erişim yetkiniz yok.", "NOT_FOUND");

        return ServiceResult<DailyReport>.Ok(report);
    }

    public async Task<ServiceResult<DailyReport>> GetForMentorAsync(Guid mentorUserId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await _db.DailyReports
            .AsNoTracking()
            .Include(r => r.WorkItems)
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .FirstOrDefaultAsync(r => r.Id == reportId && !r.IsDeleted, cancellationToken);

        if (report is null || report.InternshipPeriod is null)
            return ServiceResult<DailyReport>.Fail("Rapor bulunamadı.", "NOT_FOUND");

        if (report.InternshipPeriod.MentorUserId != mentorUserId)
            return ServiceResult<DailyReport>.Fail("Bu rapor size atanmış bir stajyere ait değil.", "FORBIDDEN");

        return ServiceResult<DailyReport>.Ok(report);
    }

    public async Task<IReadOnlyList<DailyReport>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.DailyReports.AsNoTracking()
            .Include(r => r.WorkItems)
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(r => r.InternshipPeriod!.InternProfile!.UserId == userId && !r.IsDeleted)
            .OrderByDescending(r => r.ReportDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DailyReport>> ListForMentorAsync(Guid mentorUserId, DailyReportStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _db.DailyReports.AsNoTracking()
            .Include(r => r.WorkItems)
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(r => r.InternshipPeriod!.MentorUserId == mentorUserId && !r.IsDeleted);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query.OrderByDescending(r => r.ReportDate).ToListAsync(cancellationToken);
    }

    private static bool IsEditable(DailyReportStatus status) =>
        status is DailyReportStatus.Draft or DailyReportStatus.RevisionRequested;

    private async Task<DailyReport?> LoadOwnedReportAsync(Guid userId, Guid reportId, CancellationToken cancellationToken)
    {
        var report = await _db.DailyReports
            .Include(r => r.WorkItems)
            .Include(r => r.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .FirstOrDefaultAsync(r => r.Id == reportId && !r.IsDeleted, cancellationToken);

        if (report is null || report.InternshipPeriod?.InternProfile?.UserId != userId)
            return null;

        return report;
    }

    private async Task<InternshipPeriod?> GetActivePeriodAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.InternshipPeriods
            .Include(p => p.InternProfile)
            .FirstOrDefaultAsync(p =>
                p.InternProfile!.UserId == userId &&
                p.Status == InternshipStatus.Active &&
                !p.IsDeleted, cancellationToken);
    }
}
