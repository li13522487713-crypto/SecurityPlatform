using Atlas.Core.Messaging;
using Atlas.Core.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Messaging;

/// <summary>
/// 后台消息消费处理器（轮询各队列，分发给注册的处理器）
/// </summary>
public sealed class MessageQueueProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageQueueProcessorHostedService> _logger;
    private readonly ISetupStateProvider _setupStateProvider;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public MessageQueueProcessorHostedService(
        IServiceProvider serviceProvider,
        ILogger<MessageQueueProcessorHostedService> logger,
        ISetupStateProvider setupStateProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _setupStateProvider = setupStateProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _setupStateProvider.WaitForReadyAsync(stoppingToken);
        _logger.LogInformation("MessageQueueProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<IMessageQueue>();
                var handlers = scope.ServiceProvider.GetServices<IQueueMessageHandler>();

                var handlerMap = handlers.GroupBy(h => h.QueueName)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var (queueName, queueHandlers) in handlerMap)
                {
                    await ProcessQueueAsync(queue, queueName, queueHandlers, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "MessageQueueProcessor encountered an error");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessQueueAsync(
        IMessageQueue queue,
        string queueName,
        List<IQueueMessageHandler> handlers,
        CancellationToken cancellationToken)
    {
        var messages = await queue.DequeueAsync(queueName, 10, cancellationToken);
        foreach (var msg in messages)
        {
            var handler = handlers.FirstOrDefault(h =>
                string.IsNullOrEmpty(h.MessageType) || h.MessageType == msg.MessageType);

            if (handler is null)
            {
                await queue.AcknowledgeAsync(msg.Id, cancellationToken);
                continue;
            }

            try
            {
                await handler.HandleAsync(msg, cancellationToken);
                await queue.AcknowledgeAsync(msg.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Queue message {Id} failed, will retry", msg.Id);
                await queue.RejectAsync(msg.Id, requeue: true, ex.Message, cancellationToken);
            }
        }
    }
}

/// <summary>
/// 消息处理器接口
/// </summary>
public interface IQueueMessageHandler
{
    string QueueName { get; }
    string? MessageType { get; }
    Task HandleAsync(QueueMessageItem message, CancellationToken cancellationToken);
}
