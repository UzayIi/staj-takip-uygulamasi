using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;

namespace Staj360.Infrastructure.Services;

/// <summary>
/// AuditLog kayıtlarını yapılandırılan saklama süresinden (Audit:RetentionDays) eski olanları siler.
/// </summary>
public class AuditRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuditRetentionService> _logger;

    public AuditRetentionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AuditRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // İlk çalıştırmayı kısa gecikmeyle yap; ardından günde bir.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Audit saklama temizliği başarısız.");
            }

            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var days = _configuration.GetValue("Audit:RetentionDays", 90);
        if (days < 1) days = 90;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var cutoff = clock.UtcNow.AddDays(-days);

        var old = await db.AuditLogs.Where(a => a.CreatedAtUtc < cutoff).ToListAsync(cancellationToken);
        if (old.Count == 0)
            return 0;

        db.AuditLogs.RemoveRange(old);
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Audit saklama: {Count} kayıt silindi (eşik {Days} gün).", old.Count, days);
        return old.Count;
    }
}
