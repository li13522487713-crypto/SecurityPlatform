using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Background service that periodically removes expired or revoked sessions and refresh tokens.
/// This prevents the database from growing indefinitely with stale authentication data.
/// </summary>
public sealed class SessionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupHostedService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Keep expired/revoked records for this many days before deleting (grace period for debugging).
    /// </summary>
    private const int GracePeriodDays = 7;

    /// <summary>
    /// How often to run the cleanup (every 6 hours).
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    public SessionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionCleanupHostedService> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionCleanupHostedService started. Grace period: {Days} days, Check interval: {Hours}h",
            GracePeriodDays, CheckInterval.TotalHours);

        // Wait for database initialization
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired sessions and tokens");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        var cutoff = _timeProvider.GetUtcNow().AddDays(-GracePeriodDays);

        // Clean up revoked sessions (revoked more than grace period ago)
        var revokedSessions = await db.Deleteable<AuthSession>()
            .Where(x => x.RevokedAt != null && x.RevokedAt < cutoff)
            .ExecuteCommandAsync(cancellationToken);

        // Clean up expired sessions (expired more than grace period ago)
        var expiredSessions = await db.Deleteable<AuthSession>()
            .Where(x => x.ExpiresAt < cutoff)
            .ExecuteCommandAsync(cancellationToken);

        // Clean up revoked refresh tokens
        var revokedTokens = await db.Deleteable<RefreshToken>()
            .Where(x => x.RevokedAt != null && x.RevokedAt < cutoff)
            .ExecuteCommandAsync(cancellationToken);

        // Clean up expired refresh tokens
        var expiredTokens = await db.Deleteable<RefreshToken>()
            .Where(x => x.ExpiresAt < cutoff)
            .ExecuteCommandAsync(cancellationToken);

        var total = revokedSessions + expiredSessions + revokedTokens + expiredTokens;
        if (total > 0)
        {
            _logger.LogInformation(
                "Session cleanup: {RevokedSessions} revoked sessions, {ExpiredSessions} expired sessions, " +
                "{RevokedTokens} revoked tokens, {ExpiredTokens} expired tokens",
                revokedSessions, expiredSessions, revokedTokens, expiredTokens);
        }
    }
}
