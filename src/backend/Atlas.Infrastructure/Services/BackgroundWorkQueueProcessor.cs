using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Hosted service that reads work items from <see cref="BackgroundWorkQueue"/> and
/// executes each in a dedicated DI scope. This ensures that scoped services
/// (e.g., repositories, DbContext) remain valid for the lifetime of the work item.
/// </summary>
public sealed class BackgroundWorkQueueProcessor : BackgroundService
{
    private readonly BackgroundWorkQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundWorkQueueProcessor> _logger;

    public BackgroundWorkQueueProcessor(
        BackgroundWorkQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundWorkQueueProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background work queue processor started.");

        await foreach (var workItem in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing background work item.");
            }
        }

        _logger.LogInformation("Background work queue processor stopped.");
    }
}
