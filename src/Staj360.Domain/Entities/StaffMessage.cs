using Staj360.Domain.Entities;

namespace Staj360.Domain.Entities;

/// <summary>
/// Yönetici–danışman mesajı. İçerik audit log'a yazılmaz.
/// Fiziksel silme yerine kullanıcı bazlı arşivleme kullanılır.
/// </summary>
public class StaffMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public bool IsRead { get; set; }

    public Guid? ParentMessageId { get; set; }
    public StaffMessage? ParentMessage { get; set; }

    public bool ArchivedBySender { get; set; }
    public bool ArchivedByRecipient { get; set; }
}
