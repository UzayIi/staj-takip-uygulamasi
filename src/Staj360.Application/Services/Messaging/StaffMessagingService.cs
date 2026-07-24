using System.Net;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Messaging;

public class StaffMessagingService : IStaffMessagingService
{
    private const int MaxSubjectLength = 200;
    private const int MaxBodyLength = 4000;

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;
    private readonly IUserDisplayLookup _users;

    public StaffMessagingService(
        IApplicationDbContext db,
        IClock clock,
        IAuditLogService audit,
        IUserDisplayLookup users)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _users = users;
    }

    public async Task<IReadOnlyList<EligibleRecipientDto>> GetEligibleRecipientsAsync(
        Guid senderUserId, CancellationToken cancellationToken = default)
    {
        var managerUnits = await GetActiveManagerUnitIdsAsync(senderUserId, cancellationToken);
        var advisorUnits = await GetActiveAdvisorUnitIdsAsync(senderUserId, cancellationToken);

        // Yönetici → aynı birimdeki danışmanlar; danışman → aynı birimdeki yöneticiler.
        if (managerUnits.Count > 0)
            return await BuildRecipientsFromAdvisorSideAsync(managerUnits, cancellationToken);
        if (advisorUnits.Count > 0)
            return await BuildRecipientsFromManagerSideAsync(advisorUnits, cancellationToken);

        return Array.Empty<EligibleRecipientDto>();
    }

    public async Task<ServiceResult<StaffMessage>> SendAsync(
        Guid senderUserId, SendStaffMessageCommand command, CancellationToken cancellationToken = default)
    {
        if (command.RecipientUserId == Guid.Empty || command.RecipientUserId == senderUserId)
            return ServiceResult<StaffMessage>.Fail("Geçerli bir alıcı seçiniz.", "VALIDATION");

        var subjectRaw = (command.Subject ?? string.Empty).Trim();
        var bodyRaw = (command.Body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(subjectRaw))
            return ServiceResult<StaffMessage>.Fail("Konu zorunludur.", "VALIDATION");
        if (string.IsNullOrWhiteSpace(bodyRaw))
            return ServiceResult<StaffMessage>.Fail("Mesaj içeriği zorunludur.", "VALIDATION");
        if (subjectRaw.Length > MaxSubjectLength)
            return ServiceResult<StaffMessage>.Fail($"Konu en fazla {MaxSubjectLength} karakter olabilir.", "VALIDATION");
        if (bodyRaw.Length > MaxBodyLength)
            return ServiceResult<StaffMessage>.Fail($"Mesaj en fazla {MaxBodyLength} karakter olabilir.", "VALIDATION");

        var eligible = await GetEligibleRecipientsAsync(senderUserId, cancellationToken);
        var recipient = eligible.FirstOrDefault(r => r.UserId == command.RecipientUserId);
        if (recipient is null)
            return ServiceResult<StaffMessage>.Fail("Bu kullanıcıya mesaj gönderme yetkiniz yok.", "FORBIDDEN");

        if (!recipient.SharedUnitIds.Contains(command.OrganizationUnitId))
            return ServiceResult<StaffMessage>.Fail("Seçilen birim, sizin ve alıcının ortak aktif ataması değil.", "VALIDATION");

        var unitOk = await _db.OrganizationUnits.AsNoTracking()
            .AnyAsync(u => u.Id == command.OrganizationUnitId
                           && !u.IsDeleted
                           && u.IsActive
                           && u.UnitType == OrganizationUnitType.Branch, cancellationToken);
        if (!unitOk)
            return ServiceResult<StaffMessage>.Fail("Geçersiz veya pasif birim.", "VALIDATION");

        Guid threadId;
        Guid? parentId = command.ParentMessageId;
        if (parentId.HasValue)
        {
            var parent = await _db.StaffMessages
                .FirstOrDefaultAsync(m => m.Id == parentId.Value, cancellationToken);
            if (parent is null)
                return ServiceResult<StaffMessage>.Fail("Yanıtlanacak mesaj bulunamadı.", "NOT_FOUND");
            if (parent.SenderUserId != senderUserId && parent.RecipientUserId != senderUserId)
                return ServiceResult<StaffMessage>.Fail("Bu konuşmaya yanıt verme yetkiniz yok.", "FORBIDDEN");
            if (parent.OrganizationUnitId != command.OrganizationUnitId)
                return ServiceResult<StaffMessage>.Fail("Yanıt aynı birim üzerinde olmalıdır.", "VALIDATION");
            threadId = parent.ThreadId;
        }
        else
        {
            threadId = Guid.NewGuid();
        }

        var entity = new StaffMessage
        {
            ThreadId = threadId,
            SenderUserId = senderUserId,
            RecipientUserId = command.RecipientUserId,
            OrganizationUnitId = command.OrganizationUnitId,
            Subject = WebUtility.HtmlEncode(subjectRaw),
            Body = WebUtility.HtmlEncode(bodyRaw),
            SentAtUtc = _clock.UtcNow,
            IsRead = false,
            ParentMessageId = parentId
        };

        _db.StaffMessages.Add(entity);

        var senderInfo = await _users.GetByIdsAsync([senderUserId], cancellationToken);
        senderInfo.TryGetValue(senderUserId, out var senderDisplay);
        var senderName = senderDisplay?.FullName ?? "Kullanıcı";

        _db.Notifications.Add(new Notification
        {
            UserId = command.RecipientUserId,
            Title = "Yeni mesaj",
            Message = $"{senderName} size bir mesaj gönderdi.",
            Type = NotificationType.Info,
            IsRead = false
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Audit: yalnızca taraflar + birim; konu/gövde asla yazılmaz.
        await _audit.LogAsync(
            nameof(StaffMessage),
            entity.Id.ToString(),
            "MessageSent",
            newValues: new
            {
                SenderUserId = senderUserId,
                RecipientUserId = command.RecipientUserId,
                OrganizationUnitId = command.OrganizationUnitId,
                ThreadId = threadId,
                HasParent = parentId.HasValue
            },
            organizationUnitId: command.OrganizationUnitId,
            safeDescription: "Personel mesajı gönderildi.",
            cancellationToken: cancellationToken);

        return ServiceResult<StaffMessage>.Ok(entity);
    }

    public async Task<ServiceResult> MarkReadAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var msg = await _db.StaffMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (msg is null)
            return ServiceResult.Fail("Mesaj bulunamadı.", "NOT_FOUND");
        if (msg.RecipientUserId != userId)
            return ServiceResult.Fail("Bu mesajı okundu işaretleme yetkiniz yok.", "FORBIDDEN");

        if (!msg.IsRead)
        {
            msg.IsRead = true;
            msg.ReadAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ArchiveAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var msg = await _db.StaffMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (msg is null)
            return ServiceResult.Fail("Mesaj bulunamadı.", "NOT_FOUND");

        if (msg.SenderUserId == userId)
            msg.ArchivedBySender = true;
        else if (msg.RecipientUserId == userId)
            msg.ArchivedByRecipient = true;
        else
            return ServiceResult.Fail("Bu mesajı arşivleme yetkiniz yok.", "FORBIDDEN");

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public Task<IReadOnlyList<StaffMessageListItemDto>> ListInboxAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ListAsync(userId, incoming: true, unreadOnly: false, cancellationToken);

    public Task<IReadOnlyList<StaffMessageListItemDto>> ListSentAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ListAsync(userId, incoming: false, unreadOnly: false, cancellationToken);

    public Task<IReadOnlyList<StaffMessageListItemDto>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ListAsync(userId, incoming: true, unreadOnly: true, cancellationToken);

    public async Task<ServiceResult<StaffMessageThreadDto>> GetDetailsAsync(
        Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var anchor = await _db.StaffMessages.AsNoTracking()
            .Include(m => m.OrganizationUnit)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (anchor is null)
            return ServiceResult<StaffMessageThreadDto>.Fail("Mesaj bulunamadı.", "NOT_FOUND");
        if (anchor.SenderUserId != userId && anchor.RecipientUserId != userId)
            return ServiceResult<StaffMessageThreadDto>.Fail("Bu mesajı görüntüleme yetkiniz yok.", "FORBIDDEN");

        var messages = await _db.StaffMessages.AsNoTracking()
            .Where(m => m.ThreadId == anchor.ThreadId)
            .OrderBy(m => m.SentAtUtc)
            .ToListAsync(cancellationToken);

        // Alıcı tarafındaysa okundu işaretle.
        var unread = await _db.StaffMessages
            .Where(m => m.ThreadId == anchor.ThreadId && m.RecipientUserId == userId && !m.IsRead)
            .ToListAsync(cancellationToken);
        if (unread.Count > 0)
        {
            var now = _clock.UtcNow;
            foreach (var m in unread)
            {
                m.IsRead = true;
                m.ReadAtUtc = now;
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        var otherId = anchor.SenderUserId == userId ? anchor.RecipientUserId : anchor.SenderUserId;
        var names = await _users.GetByIdsAsync([otherId], cancellationToken);
        names.TryGetValue(otherId, out var other);

        return ServiceResult<StaffMessageThreadDto>.Ok(new StaffMessageThreadDto(
            anchor.ThreadId,
            anchor.Subject,
            anchor.OrganizationUnitId,
            anchor.OrganizationUnit?.Name ?? "—",
            otherId,
            other?.FullName ?? "—",
            messages));
    }

    private async Task<IReadOnlyList<StaffMessageListItemDto>> ListAsync(
        Guid userId, bool incoming, bool unreadOnly, CancellationToken cancellationToken)
    {
        var query = _db.StaffMessages.AsNoTracking()
            .Include(m => m.OrganizationUnit)
            .AsQueryable();

        if (incoming)
        {
            query = query.Where(m => m.RecipientUserId == userId && !m.ArchivedByRecipient);
            if (unreadOnly)
                query = query.Where(m => !m.IsRead);
        }
        else
        {
            query = query.Where(m => m.SenderUserId == userId && !m.ArchivedBySender);
        }

        var items = await query.OrderByDescending(m => m.SentAtUtc).Take(200).ToListAsync(cancellationToken);
        var otherIds = items.Select(m => incoming ? m.SenderUserId : m.RecipientUserId).Distinct().ToList();
        var names = await _users.GetByIdsAsync(otherIds, cancellationToken);

        return items.Select(m =>
        {
            var otherId = incoming ? m.SenderUserId : m.RecipientUserId;
            names.TryGetValue(otherId, out var info);
            return new StaffMessageListItemDto(
                m.Id,
                m.ThreadId,
                otherId,
                info?.FullName ?? "—",
                m.OrganizationUnitId,
                m.OrganizationUnit?.Name ?? "—",
                m.Subject,
                m.SentAtUtc,
                m.IsRead,
                incoming);
        }).ToList();
    }

    private async Task<IReadOnlyList<EligibleRecipientDto>> BuildRecipientsFromAdvisorSideAsync(
        IReadOnlyCollection<Guid> managerUnitIds, CancellationToken cancellationToken)
    {
        var advisors = await _db.AdvisorUnitAssignments.AsNoTracking()
            .Include(a => a.OrganizationUnit)
            .Where(a => a.IsActive && !a.IsDeleted && managerUnitIds.Contains(a.OrganizationUnitId))
            .ToListAsync(cancellationToken);

        return await GroupRecipientsAsync(advisors.Select(a => (a.AdvisorUserId, a.OrganizationUnitId, a.OrganizationUnit?.Name ?? "—")), cancellationToken);
    }

    private async Task<IReadOnlyList<EligibleRecipientDto>> BuildRecipientsFromManagerSideAsync(
        IReadOnlyCollection<Guid> advisorUnitIds, CancellationToken cancellationToken)
    {
        var managers = await _db.ManagerUnitAssignments.AsNoTracking()
            .Include(a => a.OrganizationUnit)
            .Where(a => a.IsActive && !a.IsDeleted && advisorUnitIds.Contains(a.OrganizationUnitId))
            .ToListAsync(cancellationToken);

        return await GroupRecipientsAsync(managers.Select(a => (a.ManagerUserId, a.OrganizationUnitId, a.OrganizationUnit?.Name ?? "—")), cancellationToken);
    }

    private async Task<IReadOnlyList<EligibleRecipientDto>> GroupRecipientsAsync(
        IEnumerable<(Guid UserId, Guid UnitId, string UnitName)> rows,
        CancellationToken cancellationToken)
    {
        var grouped = rows
            .GroupBy(r => r.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Units = g.Select(x => (x.UnitId, x.UnitName)).Distinct().ToList()
            })
            .ToList();

        if (grouped.Count == 0)
            return Array.Empty<EligibleRecipientDto>();

        var names = await _users.GetByIdsAsync(grouped.Select(g => g.UserId), cancellationToken);
        return grouped
            .Select(g =>
            {
                names.TryGetValue(g.UserId, out var info);
                return new EligibleRecipientDto(
                    g.UserId,
                    info?.FullName ?? "—",
                    info?.Email ?? string.Empty,
                    g.Units.Select(u => u.UnitId).ToList(),
                    g.Units.Select(u => u.UnitName).ToList());
            })
            .OrderBy(r => r.FullName)
            .ToList();
    }

    private async Task<List<Guid>> GetActiveManagerUnitIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.ManagerUnitAssignments.AsNoTracking()
            .Where(a => a.ManagerUserId == userId && a.IsActive && !a.IsDeleted)
            .Select(a => a.OrganizationUnitId)
            .ToListAsync(cancellationToken);

    private async Task<List<Guid>> GetActiveAdvisorUnitIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.AdvisorUnitAssignments.AsNoTracking()
            .Where(a => a.AdvisorUserId == userId && a.IsActive && !a.IsDeleted)
            .Select(a => a.OrganizationUnitId)
            .ToListAsync(cancellationToken);
}
