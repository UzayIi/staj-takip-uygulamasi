namespace Staj360.Application.Abstractions;

/// <summary>
/// Kritik işlemleri denetim kaydına yazar. Parola, API anahtarı, token, T.C. kimlik
/// numarası ve hassas belge içeriği asla buraya gönderilmemelidir.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        string entityName,
        string entityId,
        string action,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}
