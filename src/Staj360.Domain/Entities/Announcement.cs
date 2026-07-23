using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

public class Announcement : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    // Oluşturan kullanıcı AuditableEntity.CreatedByUserId üzerinden tutulur.
    public bool IsActive { get; set; } = true;
}
