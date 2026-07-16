namespace Staj360.Domain.Common;

/// <summary>
/// Tüm ana varlıklar için ortak denetim ve soft-delete alanlarını taşır.
/// Kullanıcı bağlantıları Identity'ye bağımlı olmamak için Guid UserId olarak tutulur.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    // Soft delete: tarihsel kayıtların ve raporların bozulmaması için fiziksel silme yerine kullanılır.
    public bool IsDeleted { get; set; }

    // Optimistic concurrency
    public byte[]? RowVersion { get; set; }
}
