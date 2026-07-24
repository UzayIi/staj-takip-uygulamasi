namespace Staj360.Application.Abstractions;

/// <summary>
/// Kritik işlemleri denetim kaydına yazar. Parola, API anahtarı, token, T.C. kimlik,
/// rapor/mesaj içeriği asla buraya gönderilmemelidir. Hata ana işlemi bozmaz.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        string entityName,
        string entityId,
        string action,
        object? oldValues = null,
        object? newValues = null,
        Guid? organizationUnitId = null,
        string? safeDescription = null,
        bool isSuccessful = true,
        string? failureReasonCode = null,
        Guid? actorUserId = null,
        string? actorNameSnapshot = null,
        string? actorRoleSnapshot = null,
        CancellationToken cancellationToken = default);
}
