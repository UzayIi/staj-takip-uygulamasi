namespace Staj360.Domain.Entities;

/// <summary>
/// Denetim kaydı. Parola, API anahtarı, token, T.C. kimlik numarası ve hassas belge
/// içeriği burada saklanmaz. Değiştirilmez kayıt olduğundan AuditableEntity'den türemez.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
