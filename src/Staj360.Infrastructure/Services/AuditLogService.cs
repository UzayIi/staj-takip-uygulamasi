using System.Text.Json;
using Staj360.Application.Abstractions;
using Staj360.Domain.Entities;

namespace Staj360.Infrastructure.Services;

/// <summary>
/// Denetim kaydı yazar. Hassas alanlar (parola, token, API anahtarı, T.C. kimlik,
/// belge içeriği) çağıran taraftan gönderilmemelidir.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;

    public AuditLogService(IApplicationDbContext db, IClock clock, ICurrentUserService currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string entityName, string entityId, string action,
        object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            UserId = _currentUser.UserId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = _currentUser.IpAddress,
            CreatedAtUtc = _clock.UtcNow
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
