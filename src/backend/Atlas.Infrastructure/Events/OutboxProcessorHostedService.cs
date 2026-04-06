using Atlas.Application.Events;
using Atlas.Core.Events;
using Atlas.Core.Setup;
using Atlas.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Events;

/// <summary>
/// Outbox 消息后台处理服务。
/// 定时扫描待处理/失败的 Outbox 消息，分发给对应的集成事件 handler（含重试与死信逻辑）。
/// </summary>
public sealed class OutboxProcessorHostedService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorHostedService> _logger;
    private readonly ISetupStateProvider _setupStateProvider;

    public OutboxProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorHostedService> logger,
        ISetupStateProvider setupStateProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _setupStateProvider = setupStateProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _setupStateProvider.WaitForReadyAsync(stoppingToken);

        _logger.LogInformation("OutboxProcessorHostedService started, polling every {Interval}s", PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var now = DateTimeOffset.UtcNow;

            var messages = await repository.LockPendingAsync(BatchSize, now, cancellationToken);
            if (messages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Processing {Count} outbox messages", messages.Count);

            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            foreach (var message in messages)
            {
                await ProcessMessageAsync(repository, eventBus, message, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OutboxProcessor batch processing failed");
        }
    }

    private async Task ProcessMessageAsync(
        IOutboxRepository repository,
        IEventBus eventBus,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            // 将 Outbox 消息重新包装为 OutboxDeliveryEvent 发布给集成事件 handler
            var deliveryEvent = new OutboxDeliveryEvent(message);
            await eventBus.PublishAsync(deliveryEvent, cancellationToken);

            message.Status = OutboxMessageStatus.Completed;
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.ErrorMessage = null;

            await repository.UpdateAsync(message, cancellationToken);

            _logger.LogDebug(
                "Outbox message {MessageId} ({EventType}) delivered successfully",
                message.Id, message.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Outbox message {MessageId} ({EventType}) delivery failed (retry {RetryCount}/{MaxRetries})",
                message.Id, message.EventType, message.RetryCount, message.MaxRetries);

            message.RetryCount++;
            message.ErrorMessage = ex.Message;

            if (message.RetryCount >= message.MaxRetries)
            {
                message.Status = OutboxMessageStatus.DeadLettered;
                _logger.LogError(
                    "Outbox message {MessageId} ({EventType}) moved to dead-letter after {Retries} retries",
                    message.Id, message.EventType, message.RetryCount);
            }
            else
            {
                message.Status = OutboxMessageStatus.Failed;
                message.NextRetryAt = DateTimeOffset.UtcNow.Add(CalculateBackoff(message.RetryCount));
            }

            await repository.UpdateAsync(message, cancellationToken);
        }
    }

    /// <summary>指数退避：1s, 5s, 30s, 5min, 30min</summary>
    private static TimeSpan CalculateBackoff(int retryCount)
    {
        return retryCount switch
        {
            1 => TimeSpan.FromSeconds(1),
            2 => TimeSpan.FromSeconds(5),
            3 => TimeSpan.FromSeconds(30),
            4 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(30)
        };
    }
}
