using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.BackgroundTasks;

/// <summary>
/// 可运行实例轮询器 - 定时轮询可运行的工作流和事件
/// </summary>
public class RunnablePoller : IBackgroundTask
{
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly IQueueProvider _queueProvider;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RunnablePoller> _logger;
    private readonly TimeSpan _pollInterval;

    private Task? _runTask;
    private CancellationTokenSource? _cts;

    public RunnablePoller(
        IPersistenceProvider persistenceProvider,
        IQueueProvider queueProvider,
        IDistributedLockProvider lockProvider,
        IDateTimeProvider dateTimeProvider,
        ILogger<RunnablePoller> logger)
    {
        _persistenceProvider = persistenceProvider;
        _queueProvider = queueProvider;
        _lockProvider = lockProvider;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(10); // 默认10秒轮询一次
    }

    public Task Start(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = Task.Run(() => Run(_cts.Token), _cts.Token);
        _logger.LogInformation("RunnablePoller 已启动");
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

        _logger.LogInformation("RunnablePoller 已停止");
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollRunnableWorkflows(cancellationToken);
                await PollRunnableEvents(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RunnablePoller 轮询时发生错误");
            }

            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollRunnableWorkflows(CancellationToken cancellationToken)
    {
        const string lockId = "poll-runnable-workflows";

        using var activity = WorkflowActivityTracing.StartPoll("workflows");

        var lockAcquired = await _lockProvider.AcquireLock(lockId, cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogDebug("无法获取轮询工作流的锁，跳过此次轮询");
            return;
        }

        try
        {
            var now = _dateTimeProvider.UtcNow;
            var runnableInstances = await _persistenceProvider.GetRunnableInstancesAsync(now, cancellationToken);

            var instances = runnableInstances.ToList();

            if (instances.Count > 0)
            {
                _logger.LogDebug("发现 {Count} 个可运行的工作流实例", instances.Count);

                foreach (var instance in instances)
                {
                    await _queueProvider.QueueWork(instance.Id, QueueType.Workflow);
                }
            }
        }
        finally
        {
            await _lockProvider.ReleaseLock(lockId);
        }
    }

    private async Task PollRunnableEvents(CancellationToken cancellationToken)
    {
        const string lockId = "poll-runnable-events";

        using var activity = WorkflowActivityTracing.StartPoll("events");

        var lockAcquired = await _lockProvider.AcquireLock(lockId, cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogDebug("无法获取轮询事件的锁，跳过此次轮询");
            return;
        }

        try
        {
            var now = _dateTimeProvider.UtcNow;
            var runnableEvents = await _persistenceProvider.GetRunnableEventsAsync(now, cancellationToken);

            var events = runnableEvents.ToList();

            if (events.Count > 0)
            {
                _logger.LogDebug("发现 {Count} 个未处理的事件", events.Count);

                foreach (var eventId in events)
                {
                    await _queueProvider.QueueWork(eventId, QueueType.Event);
                }
            }
        }
        finally
        {
            await _lockProvider.ReleaseLock(lockId);
        }
    }
}
