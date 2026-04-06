using Atlas.Core.Setup;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Background service that periodically deletes audit records older than the configured retention period.
/// Default retention is 180 days (6 months) per 等保2.0 requirements.
/// </summary>
public sealed class AuditRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRetentionHostedService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ISetupStateProvider _setupStateProvider;

    /// <summary>
    /// Retention period in days. Defaults to 180 (6 months).
    /// </summary>
    private const int RetentionDays = 180;

    /// <summary>
    /// How often to run the cleanup check (once per day).
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public AuditRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditRetentionHostedService> logger,
        TimeProvider timeProvider,
        ISetupStateProvider setupStateProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
        _setupStateProvider = setupStateProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _setupStateProvider.WaitForReadyAsync(stoppingToken);

        _logger.LogInformation("AuditRetentionHostedService started. Retention: {Days} days, Check interval: {Hours}h",
            RetentionDays, CheckInterval.TotalHours);

        // Wait a bit on startup to let the database initialize first
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAuditRecordsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired audit records");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredAuditRecordsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        var cutoff = _timeProvider.GetUtcNow().AddDays(-RetentionDays);

        var deletedCount = await db.Deleteable<AuditRecord>()
            .Where(x => x.OccurredAt < cutoff)
            .ExecuteCommandAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} audit records older than {Cutoff:yyyy-MM-dd}", deletedCount, cutoff);
        }
    }
}
