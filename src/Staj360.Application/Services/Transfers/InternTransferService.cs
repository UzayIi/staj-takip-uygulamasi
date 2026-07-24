using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Transfers;

public record CreateTransferCommand(
    Guid InternProfileId,
    Guid TargetOrganizationUnitId,
    string? RequestNote,
    DateOnly? PlannedStartDate = null,
    Guid? TargetAdvisorUserId = null,
    bool ExecuteImmediatelyIfSameManager = false);

public record DecideTransferCommand(
    Guid TransferRequestId,
    bool Approve,
    Guid? TargetAdvisorUserId,
    string? DecisionNote,
    byte[]? RowVersion);

public interface IInternTransferService
{
    /// <summary>Yalnızca kaynak birimin yöneticisi transfer talebi oluşturabilir.</summary>
    Task<ServiceResult<InternTransferRequest>> CreateAsync(Guid actorUserId, CreateTransferCommand command, CancellationToken cancellationToken = default);

    /// <summary>Yalnızca hedef (veya her iki birimin) yöneticisi karar verebilir. Admin karar veremez.</summary>
    Task<ServiceResult> DecideAsync(Guid actorUserId, DecideTransferCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InternTransferRequest>> ListPendingForManagerAsync(Guid managerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternTransferRequest>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternTransferRequest>> ListCompletedForInternAsync(Guid internUserId, CancellationToken cancellationToken = default);
    Task<InternTransferRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

public class InternTransferService : IInternTransferService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ITimeZoneService _timeZone;
    private readonly IUnitAssignmentService _assignments;
    private readonly IAuditLogService _audit;

    public InternTransferService(
        IApplicationDbContext db,
        IClock clock,
        ITimeZoneService timeZone,
        IUnitAssignmentService assignments,
        IAuditLogService audit)
    {
        _db = db;
        _clock = clock;
        _timeZone = timeZone;
        _assignments = assignments;
        _audit = audit;
    }

    public async Task<ServiceResult<InternTransferRequest>> CreateAsync(
        Guid actorUserId, CreateTransferCommand command, CancellationToken cancellationToken = default)
    {
        var profile = await _db.InternProfiles
            .Include(p => p.InternshipPeriods)
            .FirstOrDefaultAsync(p => p.Id == command.InternProfileId && !p.IsDeleted, cancellationToken);
        if (profile is null)
            return ServiceResult<InternTransferRequest>.Fail("Stajyer bulunamadı.", "NOT_FOUND");

        var sourceUnitId = profile.CurrentOrganizationUnitId;
        if (sourceUnitId == command.TargetOrganizationUnitId)
            return ServiceResult<InternTransferRequest>.Fail("Kaynak ve hedef birim aynı olamaz.", "SAME_UNIT");

        var target = await _db.OrganizationUnits.FirstOrDefaultAsync(
            u => u.Id == command.TargetOrganizationUnitId && u.UnitType == OrganizationUnitType.Branch && u.IsActive && !u.IsDeleted, cancellationToken);
        if (target is null)
            return ServiceResult<InternTransferRequest>.Fail("Hedef şube bulunamadı veya seçilebilir değil.", "INVALID_TARGET");

        if (!await _assignments.IsManagerOfUnitAsync(actorUserId, sourceUnitId, cancellationToken))
            return ServiceResult<InternTransferRequest>.Fail("Kaynak birim sizin yetki alanınızda değil. Yalnızca sorumlu olduğunuz birimdeki stajyerleri transfer edebilirsiniz.", "FORBIDDEN");

        var pending = await _db.InternTransferRequests.AnyAsync(
            t => t.InternProfileId == profile.Id && t.Status == TransferRequestStatus.Pending && !t.IsDeleted, cancellationToken);
        if (pending)
            return ServiceResult<InternTransferRequest>.Fail("Bu stajyer için bekleyen bir transfer talebi var.", "PENDING_EXISTS");

        var today = _timeZone.LocalDate(_clock.UtcNow);
        var planned = command.PlannedStartDate ?? today;
        if (planned < today)
            return ServiceResult<InternTransferRequest>.Fail("Planlanan başlangıç tarihi geçmiş olamaz.", "INVALID_DATE");

        var sameManagerScope =
            await _assignments.IsManagerOfUnitAsync(actorUserId, command.TargetOrganizationUnitId, cancellationToken);

        // Aynı yönetici her iki birimden sorumluysa doğrudan tamamlayabilir (açık onay + danışman zorunlu).
        if (command.ExecuteImmediatelyIfSameManager && sameManagerScope)
        {
            if (!command.TargetAdvisorUserId.HasValue)
                return ServiceResult<InternTransferRequest>.Fail("Doğrudan transfer için hedef danışman seçilmelidir.", "ADVISOR_REQUIRED");

            var advisorOk = await _db.AdvisorUnitAssignments.AnyAsync(
                a => a.AdvisorUserId == command.TargetAdvisorUserId && a.OrganizationUnitId == command.TargetOrganizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);
            if (!advisorOk)
                return ServiceResult<InternTransferRequest>.Fail("Seçilen danışman hedef birime atanmamış.", "INVALID_ADVISOR");

            await using var tx = _db is DbContext ef
                ? await ef.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var executed = await ExecuteTransferAsync(
                profile, sourceUnitId, command.TargetOrganizationUnitId, command.TargetAdvisorUserId.Value, planned, cancellationToken);
            if (!executed.Success)
                return ServiceResult<InternTransferRequest>.Fail(executed.ErrorMessage!, executed.ErrorCode);

            var direct = new InternTransferRequest
            {
                InternProfileId = profile.Id,
                SourceOrganizationUnitId = sourceUnitId,
                TargetOrganizationUnitId = command.TargetOrganizationUnitId,
                RequestedByUserId = actorUserId,
                Status = TransferRequestStatus.Approved,
                PlannedStartDate = planned,
                TargetAdvisorUserId = command.TargetAdvisorUserId,
                DecisionByUserId = actorUserId,
                DecisionAtUtc = _clock.UtcNow,
                RequestNote = Sanitize(command.RequestNote),
                DecisionNote = "Her iki müdürlükten de sorumlu olduğunuz için transfer doğrudan tamamlandı."
            };
            _db.InternTransferRequests.Add(direct);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult<InternTransferRequest>.Fail("Eşzamanlı işlem çakışması.", "CONCURRENCY");
            }

            await NotifyTransferPartiesAsync(direct, profile, "Stajyer transferi tamamlandı.", cancellationToken);
            await _audit.LogAsync(nameof(InternTransferRequest), direct.Id.ToString(), "ApproveDirect",
                organizationUnitId: sourceUnitId,
                safeDescription: "Aynı yönetici kapsamındaki doğrudan transfer tamamlandı.",
                cancellationToken: cancellationToken);
            return ServiceResult<InternTransferRequest>.Ok(direct);
        }

        var request = new InternTransferRequest
        {
            InternProfileId = profile.Id,
            SourceOrganizationUnitId = sourceUnitId,
            TargetOrganizationUnitId = command.TargetOrganizationUnitId,
            RequestedByUserId = actorUserId,
            Status = TransferRequestStatus.Pending,
            PlannedStartDate = planned,
            RequestNote = Sanitize(command.RequestNote)
        };
        _db.InternTransferRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyTargetManagersAsync(command.TargetOrganizationUnitId, profile.Id, "Yeni stajyer transfer talebi.", cancellationToken);
        await _audit.LogAsync(nameof(InternTransferRequest), request.Id.ToString(), "Create",
            organizationUnitId: sourceUnitId,
            safeDescription: "Transfer talebi oluşturuldu.",
            cancellationToken: cancellationToken);
        return ServiceResult<InternTransferRequest>.Ok(request);
    }

    public async Task<ServiceResult> DecideAsync(Guid actorUserId, DecideTransferCommand command, CancellationToken cancellationToken = default)
    {
        await using var tx = _db is DbContext ef
            ? await ef.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var request = await _db.InternTransferRequests
            .FirstOrDefaultAsync(t => t.Id == command.TransferRequestId && !t.IsDeleted, cancellationToken);
        if (request is null)
            return ServiceResult.Fail("Transfer talebi bulunamadı.", "NOT_FOUND");

        if (request.Status != TransferRequestStatus.Pending)
            return ServiceResult.Fail("Bu talep zaten sonuçlandırılmış.", "ALREADY_DECIDED");

        var isTargetManager = await _assignments.IsManagerOfUnitAsync(actorUserId, request.TargetOrganizationUnitId, cancellationToken);
        var isBothManager = isTargetManager
            && await _assignments.IsManagerOfUnitAsync(actorUserId, request.SourceOrganizationUnitId, cancellationToken);
        if (!isTargetManager && !isBothManager)
            return ServiceResult.Fail("Bu transferi sonuçlandırma yetkiniz yok.", "FORBIDDEN");

        if (command.RowVersion is not null)
            request.RowVersion = command.RowVersion;

        if (!command.Approve)
        {
            request.Status = TransferRequestStatus.Rejected;
            request.DecisionByUserId = actorUserId;
            request.DecisionAtUtc = _clock.UtcNow;
            request.DecisionNote = Sanitize(command.DecisionNote);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult.Fail("Talep başka bir yönetici tarafından sonuçlandırılmış.", "CONCURRENCY");
            }

            var profileReject = await _db.InternProfiles.FirstOrDefaultAsync(p => p.Id == request.InternProfileId, cancellationToken);
            if (profileReject is not null)
                await NotifyTransferPartiesAsync(request, profileReject, "Transfer talebi reddedildi.", cancellationToken);

            await _audit.LogAsync(nameof(InternTransferRequest), request.Id.ToString(), "Reject",
                organizationUnitId: request.TargetOrganizationUnitId,
                safeDescription: "Transfer talebi reddedildi.",
                cancellationToken: cancellationToken);
            return ServiceResult.Ok();
        }

        if (!command.TargetAdvisorUserId.HasValue)
            return ServiceResult.Fail("Onay için hedef danışman seçilmelidir.", "ADVISOR_REQUIRED");

        var advisorOk = await _db.AdvisorUnitAssignments.AnyAsync(
            a => a.AdvisorUserId == command.TargetAdvisorUserId && a.OrganizationUnitId == request.TargetOrganizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);
        if (!advisorOk)
            return ServiceResult.Fail("Seçilen danışman hedef birime atanmamış.", "INVALID_ADVISOR");

        var profile = await _db.InternProfiles.FirstOrDefaultAsync(p => p.Id == request.InternProfileId && !p.IsDeleted, cancellationToken);
        if (profile is null)
            return ServiceResult.Fail("Stajyer bulunamadı.", "NOT_FOUND");

        var start = request.PlannedStartDate ?? _timeZone.LocalDate(_clock.UtcNow);
        var exec = await ExecuteTransferAsync(
            profile, request.SourceOrganizationUnitId, request.TargetOrganizationUnitId,
            command.TargetAdvisorUserId.Value, start, cancellationToken);
        if (!exec.Success)
            return exec;

        request.Status = TransferRequestStatus.Approved;
        request.TargetAdvisorUserId = command.TargetAdvisorUserId;
        request.DecisionByUserId = actorUserId;
        request.DecisionAtUtc = _clock.UtcNow;
        request.DecisionNote = Sanitize(command.DecisionNote);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("Talep başka bir yönetici tarafından sonuçlandırılmış.", "CONCURRENCY");
        }

        await NotifyTransferPartiesAsync(request, profile, "Stajyer transferi onaylandı.", cancellationToken);
        await _audit.LogAsync(nameof(InternTransferRequest), request.Id.ToString(), "Approve",
            organizationUnitId: request.TargetOrganizationUnitId,
            safeDescription: "Transfer talebi onaylandı.",
            cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<InternTransferRequest>> ListPendingForManagerAsync(Guid managerUserId, CancellationToken cancellationToken = default)
    {
        var unitIds = await _assignments.GetManagerUnitIdsAsync(managerUserId, cancellationToken);
        return await _db.InternTransferRequests.AsNoTracking()
            .Include(t => t.InternProfile)
            .Include(t => t.SourceOrganizationUnit)
            .Include(t => t.TargetOrganizationUnit)
            .Where(t => t.Status == TransferRequestStatus.Pending && !t.IsDeleted
                        && (unitIds.Contains(t.TargetOrganizationUnitId) || unitIds.Contains(t.SourceOrganizationUnitId)))
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InternTransferRequest>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _db.InternTransferRequests.AsNoTracking()
            .Include(t => t.InternProfile)
            .Include(t => t.SourceOrganizationUnit)
            .Include(t => t.TargetOrganizationUnit)
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InternTransferRequest>> ListCompletedForInternAsync(Guid internUserId, CancellationToken cancellationToken = default)
    {
        var profileId = await _db.InternProfiles.AsNoTracking()
            .Where(p => p.UserId == internUserId && !p.IsDeleted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
            return Array.Empty<InternTransferRequest>();

        return await _db.InternTransferRequests.AsNoTracking()
            .Include(t => t.SourceOrganizationUnit)
            .Include(t => t.TargetOrganizationUnit)
            .Where(t => t.InternProfileId == profileId && !t.IsDeleted
                        && t.Status != TransferRequestStatus.Pending)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<InternTransferRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.InternTransferRequests.AsNoTracking()
            .Include(t => t.InternProfile)
            .Include(t => t.SourceOrganizationUnit)!.ThenInclude(u => u!.Parent)
            .Include(t => t.TargetOrganizationUnit)!.ThenInclude(u => u!.Parent)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);

    private async Task<ServiceResult> ExecuteTransferAsync(
        InternProfile profile, Guid sourceUnitId, Guid targetUnitId, Guid advisorUserId, DateOnly startDate, CancellationToken cancellationToken)
    {
        var active = await _db.InternUnitAssignments
            .FirstOrDefaultAsync(a => a.InternProfileId == profile.Id && a.IsActive && !a.IsDeleted, cancellationToken);
        if (active is not null)
        {
            active.IsActive = false;
            active.EndDate = startDate;
        }

        _db.InternUnitAssignments.Add(new InternUnitAssignment
        {
            InternProfileId = profile.Id,
            OrganizationUnitId = targetUnitId,
            AdvisorUserId = advisorUserId,
            StartDate = startDate,
            IsActive = true
        });

        profile.CurrentOrganizationUnitId = targetUnitId;

        // Eski rapor/proje OrganizationUnitId değerleri değiştirilmez.
        var period = await _db.InternshipPeriods
            .FirstOrDefaultAsync(p => p.InternProfileId == profile.Id && p.Status == InternshipStatus.Active && !p.IsDeleted, cancellationToken);
        if (period is not null)
            period.MentorUserId = advisorUserId;

        await Task.CompletedTask;
        return ServiceResult.Ok();
    }

    private async Task NotifyTargetManagersAsync(Guid targetUnitId, Guid internProfileId, string title, CancellationToken cancellationToken)
    {
        var managers = await _assignments.GetActiveManagerUserIdsForUnitAsync(targetUnitId, cancellationToken);
        foreach (var mid in managers)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = mid,
                Title = title,
                Message = "Yeni bir stajyer transfer talebi bekliyor.",
                Type = NotificationType.Info
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyTransferPartiesAsync(InternTransferRequest request, InternProfile profile, string title, CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid> { request.RequestedByUserId };
        if (request.DecisionByUserId.HasValue)
            recipients.Add(request.DecisionByUserId.Value);
        if (request.TargetAdvisorUserId.HasValue)
            recipients.Add(request.TargetAdvisorUserId.Value);
        recipients.Add(profile.UserId);

        var oldAdvisor = await _db.InternUnitAssignments.AsNoTracking()
            .Where(a => a.InternProfileId == profile.Id && !a.IsActive && a.OrganizationUnitId == request.SourceOrganizationUnitId)
            .OrderByDescending(a => a.EndDate)
            .Select(a => a.AdvisorUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (oldAdvisor != Guid.Empty)
            recipients.Add(oldAdvisor);

        foreach (var uid in recipients)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = uid,
                Title = title,
                Message = "Transfer durumu güncellendi.",
                Type = NotificationType.Info
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? Sanitize(string? input) =>
        string.IsNullOrWhiteSpace(input) ? null : System.Net.WebUtility.HtmlEncode(input.Trim());
}
