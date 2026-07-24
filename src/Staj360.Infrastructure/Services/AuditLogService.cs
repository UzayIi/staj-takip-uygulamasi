using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Staj360.Application.Abstractions;
using Staj360.Domain.Entities;

namespace Staj360.Infrastructure.Services;

/// <summary>
/// Append-only denetim kaydı. Hassas içerik yazılmaz; kayıt hataları ana işlemi bozmaz.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IApplicationDbContext db,
        IClock clock,
        ICurrentUserService currentUser,
        ILogger<AuditLogService> logger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task LogAsync(
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var actorId = actorUserId ?? _currentUser.UserId;
            var log = new AuditLog
            {
                UserId = actorId,
                ActorUserId = actorId,
                ActorNameSnapshot = Truncate(actorNameSnapshot ?? _currentUser.UserName, 200),
                ActorRoleSnapshot = Truncate(actorRoleSnapshot ?? _currentUser.PrimaryRole, 100),
                EntityName = Truncate(entityName, 150) ?? string.Empty,
                EntityId = Truncate(entityId, 100) ?? string.Empty,
                Action = Truncate(action, 100) ?? string.Empty,
                OrganizationUnitId = organizationUnitId,
                SafeDescription = Truncate(safeDescription, 500),
                OldValues = SerializeSafe(oldValues),
                NewValues = SerializeSafe(newValues),
                IpAddress = Truncate(NormalizeIp(_currentUser.IpAddress), 64),
                UserAgent = Truncate(_currentUser.UserAgent, 512),
                RequestMethod = Truncate(_currentUser.RequestMethod, 16),
                RequestPath = Truncate(_currentUser.RequestPath, 500),
                IsSuccessful = isSuccessful,
                FailureReasonCode = Truncate(failureReasonCode, 100),
                CorrelationId = Truncate(_currentUser.CorrelationId, 64),
                CreatedAtUtc = _clock.UtcNow
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit kaydı yazılamadı. Action={Action} Entity={Entity}/{EntityId}", action, entityName, entityId);
        }
    }

    private static string? SerializeSafe(object? value)
    {
        if (value is null) return null;
        try { return JsonSerializer.Serialize(value); }
        catch { return null; }
    }

    public static string? NormalizeIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        if (IPAddress.TryParse(ip, out var address))
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            return address.ToString();
        }
        return ip.Trim();
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
