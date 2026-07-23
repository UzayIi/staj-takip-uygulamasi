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
    Task<ServiceResult<InternTransferRequest>> CreateAsync(Guid actorUserId, bool isAdmin, bool isManager, bool isMentor, CreateTransferCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> DecideAsync(Guid actorUserId, bool isAdmin, DecideTransferCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternTransferRequest>> ListPendingForManagerAsync(Guid managerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternTransferRequest>> ListAllAsync(CancellationToken cancellationToken = default);
}

public class InternTransferService : IInternTransferService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IUnitAssignmentService _assignments;
    private readonly IAuditLogService _audit;

    public InternTransferService(
        IApplicationDbContext db, IClock clock, IUnitAssignmentService assignments, IAuditLogService audit)
    {
        _db = db;
        _clock = clock;
        _assignments = assignments;
        _audit = audit;
    }

    public async Task<ServiceResult<InternTransferRequest>> CreateAsync(
        Guid actorUserId, bool isAdmin, bool isManager, bool isMentor,
        CreateTransferCommand command, CancellationToken cancellationToken = default)
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
            u => u.Id == command.TargetOrganizationUnitId && u.UnitType == OrganizationUnitType.Branch && !u.IsDeleted, cancellationToken);
        if (target is null)
            return ServiceResult<InternTransferRequest>.Fail("Hedef şube bulunamadı.", "INVALID_TARGET");

        var pending = await _db.InternTransferRequests.AnyAsync(
            t => t.InternProfileId == profile.Id && t.Status == TransferRequestStatus.Pending && !t.IsDeleted, cancellationToken);
        if (pending)
            return ServiceResult<InternTransferRequest>.Fail("Bu stajyer için bekleyen bir transfer talebi var.", "PENDING_EXISTS");

        if (!isAdmin)
        {
            if (isManager)
            {
                if (!await _assignments.IsManagerOfUnitAsync(actorUserId, sourceUnitId, cancellationToken))
                    return ServiceResult<InternTransferRequest>.Fail("Kaynak birim sizin yetki alanınızda değil.", "FORBIDDEN");
            }
            else if (isMentor)
            {
                var activePeriod = profile.InternshipPeriods.FirstOrDefault(p => p.Status == InternshipStatus.Active && !p.IsDeleted);
                if (activePeriod is null || activePeriod.MentorUserId != actorUserId)
                    return ServiceResult<InternTransferRequest>.Fail("Yalnızca kendi stajyeriniz için transfer talebi oluşturabilirsiniz.", "FORBIDDEN");
            }
            else
            {
                return ServiceResult<InternTransferRequest>.Fail("Yetkisiz.", "FORBIDDEN");
            }
        }

        var sameManagerScope = isManager &&
            await _assignments.IsManagerOfUnitAsync(actorUserId, sourceUnitId, cancellationToken) &&
            await _assignments.IsManagerOfUnitAsync(actorUserId, command.TargetOrganizationUnitId, cancellationToken);

        // Admin veya aynı yönetici alanı: doğrudan transfer (advisor gerekli).
        if ((isAdmin || (isManager && command.ExecuteImmediatelyIfSameManager && sameManagerScope))
            && command.TargetAdvisorUserId.HasValue)
        {
            var executed = await ExecuteTransferAsync(
                profile, sourceUnitId, command.TargetOrganizationUnitId, command.TargetAdvisorUserId.Value, actorUserId, cancellationToken);
            if (!executed.Success)
                return ServiceResult<InternTransferRequest>.Fail(executed.ErrorMessage!, executed.ErrorCode);

            var direct = new InternTransferRequest
            {
                InternProfileId = profile.Id,
                SourceOrganizationUnitId = sourceUnitId,
                TargetOrganizationUnitId = command.TargetOrganizationUnitId,
                RequestedByUserId = actorUserId,
                Status = TransferRequestStatus.Approved,
                TargetAdvisorUserId = command.TargetAdvisorUserId,
                DecisionByUserId = actorUserId,
                DecisionAtUtc = _clock.UtcNow,
                RequestNote = command.RequestNote,
                DecisionNote = "Doğrudan transfer"
            };
            _db.InternTransferRequests.Add(direct);
            await _db.SaveChangesAsync(cancellationToken);
            await NotifyTargetManagersAsync(command.TargetOrganizationUnitId, profile.Id, "Stajyer transferi tamamlandı.", cancellationToken);
            return ServiceResult<InternTransferRequest>.Ok(direct);
        }

        var request = new InternTransferRequest
        {
            InternProfileId = profile.Id,
            SourceOrganizationUnitId = sourceUnitId,
            TargetOrganizationUnitId = command.TargetOrganizationUnitId,
            RequestedByUserId = actorUserId,
            Status = TransferRequestStatus.Pending,
            RequestNote = Sanitize(command.RequestNote)
        };
        _db.InternTransferRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyTargetManagersAsync(command.TargetOrganizationUnitId, profile.Id, "Yeni stajyer transfer talebi.", cancellationToken);
        await _audit.LogAsync(nameof(InternTransferRequest), request.Id.ToString(), "Create", cancellationToken: cancellationToken);
        return ServiceResult<InternTransferRequest>.Ok(request);
    }

    public async Task<ServiceResult> DecideAsync(Guid actorUserId, bool isAdmin, DecideTransferCommand command, CancellationToken cancellationToken = default)
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

        if (!isAdmin)
        {
            var isTargetManager = await _assignments.IsManagerOfUnitAsync(actorUserId, request.TargetOrganizationUnitId, cancellationToken);
            var isSourceManagerSameScope = await _assignments.IsManagerOfUnitAsync(actorUserId, request.SourceOrganizationUnitId, cancellationToken)
                && await _assignments.IsManagerOfUnitAsync(actorUserId, request.TargetOrganizationUnitId, cancellationToken);
            if (!isTargetManager && !isSourceManagerSameScope)
                return ServiceResult.Fail("Bu transferi sonuçlandırma yetkiniz yok.", "FORBIDDEN");
        }

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

            await _audit.LogAsync(nameof(InternTransferRequest), request.Id.ToString(), "Reject", cancellationToken: cancellationToken);
            return ServiceResult.Ok();
        }

        if (!command.TargetAdvisorUserId.HasValue)
            return ServiceResult.Fail("Onay için hedef danışman seçilmelidir.", "ADVISOR_REQUIRED");

        var advisorOk = await _db.AdvisorUnitAssignments.AnyAsync(
            a => a.AdvisorUserId == command.TargetAdvisorUserId && a.OrganizationUnitId == request.TargetOrganizationUnitId && a.IsActive && !a.IsDeleted, cancellationToken);
        if (!advisorOk && !isAdmin)
            return ServiceResult.Fail("Seçilen danışman hedef birime atanmamış.", "INVALID_ADVISOR");

        var profile = await _db.InternProfiles.FirstOrDefaultAsync(p => p.Id == request.InternProfileId && !p.IsDeleted, cancellationToken);
        if (profile is null)
            return ServiceResult.Fail("Stajyer bulunamadı.", "NOT_FOUND");

        var exec = await ExecuteTransferAsync(
            profile, request.SourceOrganizationUnitId, request.TargetOrganizationUnitId,
            command.TargetAdvisorUserId.Value, actorUserId, cancellationToken);
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

        await _audit.LogAsync(nameof(InternTransferRequest), request.Id.ToString(), "Approve", cancellationToken: cancellationToken);
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

    private async Task<ServiceResult> ExecuteTransferAsync(
        InternProfile profile, Guid sourceUnitId, Guid targetUnitId, Guid advisorUserId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var active = await _db.InternUnitAssignments
            .FirstOrDefaultAsync(a => a.InternProfileId == profile.Id && a.IsActive && !a.IsDeleted, cancellationToken);
        if (active is not null)
        {
            active.IsActive = false;
            active.EndDate = today;
        }

        _db.InternUnitAssignments.Add(new InternUnitAssignment
        {
            InternProfileId = profile.Id,
            OrganizationUnitId = targetUnitId,
            AdvisorUserId = advisorUserId,
            StartDate = today,
            IsActive = true
        });

        profile.CurrentOrganizationUnitId = targetUnitId;

        // Aktif dönem danışmanını güncelle (eski rapor/proje OrganizationUnitId değişmez).
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
                Message = $"Stajyer profili: {internProfileId}",
                Type = NotificationType.Info
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? Sanitize(string? input) =>
        string.IsNullOrWhiteSpace(input) ? null : System.Net.WebUtility.HtmlEncode(input.Trim());
}
