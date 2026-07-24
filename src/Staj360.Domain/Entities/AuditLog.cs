namespace Staj360.Domain.Entities;

/// <summary>
/// Append-only denetim kaydı. Parola, token, cookie, connection string, rapor/mesaj
/// içeriği burada saklanmaz. AuditableEntity'den türemez (düzenleme/silme yok).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Geriye dönük uyumluluk; ActorUserId ile aynı anlamdadır.</summary>
    public Guid? UserId { get; set; }

    public Guid? ActorUserId { get; set; }
    public string? ActorNameSnapshot { get; set; }
    public string? ActorRoleSnapshot { get; set; }

    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;

    public Guid? OrganizationUnitId { get; set; }
    public string? SafeDescription { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }

    public bool IsSuccessful { get; set; } = true;
    public string? FailureReasonCode { get; set; }
    public string? CorrelationId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
