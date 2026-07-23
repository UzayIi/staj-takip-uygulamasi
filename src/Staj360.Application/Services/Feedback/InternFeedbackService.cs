using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Feedback;

public record CreateFeedbackCommand(Guid AdvisorUserId, string Title, string Message);
public record ReplyFeedbackCommand(Guid FeedbackId, string ReplyMessage, FeedbackStatus NewStatus);

public interface IInternFeedbackService
{
    Task<ServiceResult<InternFeedback>> CreateAsync(Guid internUserId, CreateFeedbackCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> ReplyAsync(Guid advisorUserId, ReplyFeedbackCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateStatusAsync(Guid advisorUserId, Guid feedbackId, FeedbackStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternFeedback>> ListForInternAsync(Guid internUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternFeedback>> ListForAdvisorAsync(Guid advisorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternFeedback>> ListAllForAdminAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetAllowedAdvisorIdsAsync(Guid internUserId, CancellationToken cancellationToken = default);
}

public class InternFeedbackService : IInternFeedbackService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public InternFeedbackService(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Guid>> GetAllowedAdvisorIdsAsync(Guid internUserId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.InternProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == internUserId && !p.IsDeleted, cancellationToken);
        if (profile is null) return Array.Empty<Guid>();

        var assignment = await _db.InternUnitAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.InternProfileId == profile.Id && a.IsActive && !a.IsDeleted, cancellationToken);

        var ids = new HashSet<Guid>();
        if (assignment is not null)
            ids.Add(assignment.AdvisorUserId);

        var periodMentors = await _db.InternshipPeriods.AsNoTracking()
            .Where(p => p.InternProfileId == profile.Id && p.Status == InternshipStatus.Active && !p.IsDeleted)
            .Select(p => p.MentorUserId)
            .ToListAsync(cancellationToken);
        foreach (var m in periodMentors)
            ids.Add(m);

        return ids.ToList();
    }

    public async Task<ServiceResult<InternFeedback>> CreateAsync(Guid internUserId, CreateFeedbackCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.Message))
            return ServiceResult<InternFeedback>.Fail("Başlık ve mesaj zorunludur.", "VALIDATION");

        var profile = await _db.InternProfiles
            .FirstOrDefaultAsync(p => p.UserId == internUserId && !p.IsDeleted, cancellationToken);
        if (profile is null)
            return ServiceResult<InternFeedback>.Fail("Stajyer profili bulunamadı.", "NOT_FOUND");

        var allowed = await GetAllowedAdvisorIdsAsync(internUserId, cancellationToken);
        if (!allowed.Contains(command.AdvisorUserId))
            return ServiceResult<InternFeedback>.Fail("Yalnızca size atanmış danışmanlara geri bildirim gönderebilirsiniz.", "FORBIDDEN");

        var entity = new InternFeedback
        {
            InternProfileId = profile.Id,
            AdvisorUserId = command.AdvisorUserId,
            Title = SanitizeRequired(command.Title, 250),
            Message = SanitizeRequired(command.Message, 4000),
            Status = FeedbackStatus.Open
        };
        _db.InternFeedbacks.Add(entity);
        _db.Notifications.Add(new Notification
        {
            UserId = command.AdvisorUserId,
            Title = "Yeni geri bildirim",
            Message = entity.Title,
            Type = NotificationType.Info
        });
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<InternFeedback>.Ok(entity);
    }

    public async Task<ServiceResult> ReplyAsync(Guid advisorUserId, ReplyFeedbackCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ReplyMessage))
            return ServiceResult.Fail("Cevap metni zorunludur.", "VALIDATION");

        var fb = await _db.InternFeedbacks
            .Include(f => f.InternProfile)
            .FirstOrDefaultAsync(f => f.Id == command.FeedbackId && !f.IsDeleted, cancellationToken);
        if (fb is null)
            return ServiceResult.Fail("Geri bildirim bulunamadı.", "NOT_FOUND");
        if (fb.AdvisorUserId != advisorUserId)
            return ServiceResult.Fail("Bu geri bildirime cevap veremezsiniz.", "FORBIDDEN");

        fb.ReplyMessage = SanitizeRequired(command.ReplyMessage, 4000);
        fb.RepliedAtUtc = _clock.UtcNow;
        fb.Status = command.NewStatus is FeedbackStatus.Replied or FeedbackStatus.Resolved
            ? command.NewStatus
            : FeedbackStatus.Replied;

        if (fb.InternProfile is not null)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = fb.InternProfile.UserId,
                Title = "Geri bildiriminize cevap verildi",
                Message = fb.Title,
                Type = NotificationType.Success
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateStatusAsync(Guid advisorUserId, Guid feedbackId, FeedbackStatus status, CancellationToken cancellationToken = default)
    {
        var fb = await _db.InternFeedbacks.FirstOrDefaultAsync(f => f.Id == feedbackId && !f.IsDeleted, cancellationToken);
        if (fb is null)
            return ServiceResult.Fail("Geri bildirim bulunamadı.", "NOT_FOUND");
        if (fb.AdvisorUserId != advisorUserId)
            return ServiceResult.Fail("Yetkisiz.", "FORBIDDEN");

        fb.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<InternFeedback>> ListForInternAsync(Guid internUserId, CancellationToken cancellationToken = default)
    {
        var profileId = await _db.InternProfiles.AsNoTracking()
            .Where(p => p.UserId == internUserId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (profileId == Guid.Empty) return Array.Empty<InternFeedback>();

        return await _db.InternFeedbacks.AsNoTracking()
            .Where(f => f.InternProfileId == profileId && !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InternFeedback>> ListForAdvisorAsync(Guid advisorUserId, CancellationToken cancellationToken = default) =>
        await _db.InternFeedbacks.AsNoTracking()
            .Include(f => f.InternProfile)
            .Where(f => f.AdvisorUserId == advisorUserId && !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InternFeedback>> ListAllForAdminAsync(CancellationToken cancellationToken = default) =>
        await _db.InternFeedbacks.AsNoTracking()
            .Include(f => f.InternProfile)
            .Where(f => !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    private static string SanitizeRequired(string input, int maxLen)
    {
        var encoded = System.Net.WebUtility.HtmlEncode(input.Trim());
        return encoded.Length <= maxLen ? encoded : encoded[..maxLen];
    }
}
