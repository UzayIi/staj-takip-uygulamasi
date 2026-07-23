using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

/// <summary>Stajyer → atanmış danışman geri bildirimi (anonim değil).</summary>
public class InternFeedback : AuditableEntity
{
    public Guid InternProfileId { get; set; }
    public InternProfile? InternProfile { get; set; }

    public Guid AdvisorUserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;

    public string? ReplyMessage { get; set; }
    public DateTime? RepliedAtUtc { get; set; }

    /// <summary>İleride eskalasyon için ayrılmış; şu an kullanılmaz.</summary>
    public bool IsEscalated { get; set; }
    public Guid? EscalatedToUserId { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public string? EscalationNote { get; set; }
}
