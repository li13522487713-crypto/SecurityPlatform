using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.BackgroundTasks;

/// <summary>
/// 队列消费者抽象基类
/// </summary>
public abstract class QueueConsumer : IBackgroundTask
{
    protected readonly IQueueProvider QueueProvider;
    protected readonly ILogger Logger;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    protected QueueConsumer(IQueueProvider queueProvider, ILogger logger)
    {
        QueueProvider = queueProvider;
        Logger = logger;
    }

    /// <summary>
    /// 队列类型
    /// </summary>
    protected abstract QueueType Queue { get; }

    public Task Start(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = Task.Run(() => Run(_cts.Token), _cts.Token);
        Logger.LogInformation("{ConsumerName} 已启动", GetType().Name);
        return Task.CompletedTask;
    }

    public async Task Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        if (_runTask != null)
        {
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
                // 预期的取消异常
            }
        }

        Logger.LogInformation("{ConsumerName} 已停止", GetType().Name);
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var itemId = await QueueProvider.DequeueWork(Queue, cancellationToken);

                if (itemId == null)
                {
                    // 队列为空，等待一段时间
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                await ProcessItem(itemId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{ConsumerName} 处理工作项时发生错误", GetType().Name);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 处理工作项
    /// </summary>
    /// <param name="itemId">工作项ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected abstract Task ProcessItem(string itemId, CancellationToken cancellationToken);
}
