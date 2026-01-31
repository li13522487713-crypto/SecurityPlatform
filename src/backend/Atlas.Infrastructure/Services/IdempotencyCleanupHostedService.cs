using Atlas.Application.Abstractions;
using Atlas.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services;

public sealed class IdempotencyCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<IdempotencyOptions> _options;
    private readonly ILogger<IdempotencyCleanupHostedService> _logger;

    public IdempotencyCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = ResolveCleanupInterval();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRecordRepository>();
                var now = _timeProvider.GetUtcNow();
                var deleted = await repository.DeleteExpiredAsync(now, stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation("已清理过期幂等记录：{Count} 条", deleted);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "幂等记录清理任务执行失败");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }
    }

    private TimeSpan ResolveCleanupInterval()
    {
        var configured = TimeSpan.FromMinutes(Math.Max(1, _options.Value.CleanupIntervalMinutes));
        if (configured < MinimumInterval)
        {
            return MinimumInterval;
        }

        return configured;
    }
}
