using Staj360.Application.Abstractions;

namespace Staj360.UnitTests.TestSupport;

public sealed class NoOpAuditLogService : IAuditLogService
{
    public Task LogAsync(
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
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
