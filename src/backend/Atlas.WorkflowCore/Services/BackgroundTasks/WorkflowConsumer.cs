using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.WorkflowCore.Services.BackgroundTasks;

/// <summary>
/// 工作流消费者 - 从队列消费工作流实例并执行
/// </summary>
public class WorkflowConsumer : QueueConsumer
{
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILifeCycleEventPublisher _eventPublisher;
    private readonly IGreyList _greyList;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly WorkflowOptions _options;

    public WorkflowConsumer(
        IQueueProvider queueProvider,
        IPersistenceProvider persistenceProvider,
        IServiceScopeFactory scopeFactory,
        IDistributedLockProvider lockProvider,
        ILifeCycleEventPublisher eventPublisher,
        IGreyList greyList,
        IDateTimeProvider dateTimeProvider,
        IOptions<WorkflowOptions> options,
        ILogger<WorkflowConsumer> logger)
        : base(queueProvider, options.Value, logger)
    {
        _persistenceProvider = persistenceProvider;
        _scopeFactory = scopeFactory;
        _lockProvider = lockProvider;
        _eventPublisher = eventPublisher;
        _greyList = greyList;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
    }

    protected override QueueType Queue => QueueType.Workflow;

    protected override int MaxConcurrentItems => _options.MaxConcurrentWorkflows;

    protected override async Task ProcessItem(string itemId, CancellationToken cancellationToken)
    {
        var lockAcquired = false;
        WorkflowInstance? workflow = null;
        WorkflowExecutorResult? result = null;

        try
        {
            // 1. 获取分布式锁
            lockAcquired = await _lockProvider.AcquireLock(itemId, cancellationToken);

            if (!lockAcquired)
            {
                Logger.LogInformation("工作流 {WorkflowId} 已被锁定", itemId);
                return;
            }

            // 2. 获取工作流实例
            cancellationToken.ThrowIfCancellationRequested();
            workflow = await _persistenceProvider.GetWorkflowAsync(itemId, cancellationToken);

            if (workflow == null)
            {
                Logger.LogWarning("工作流实例 {WorkflowId} 不存在", itemId);
                return;
            }

            // 3. 添加追踪信息
            WorkflowActivityTracing.Enrich(workflow, "process");

            if (workflow.Status != WorkflowStatus.Runnable)
            {
                Logger.LogDebug("工作流 {WorkflowId} 状态为 {Status}，不可执行", itemId, workflow.Status);
                return;
            }

            // 4. 执行工作流
            try
            {
                Logger.LogDebug("开始执行工作流 {WorkflowId}", itemId);
                using var scope = _scopeFactory.CreateScope();
                var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
                result = await executor.Execute(workflow, cancellationToken);
            }
            finally
            {
                // 5. 添加执行结果追踪信息
                if (result != null)
                {
                    WorkflowActivityTracing.Enrich(result);
                }

                // 6. 持久化工作流状态和订阅
                await _persistenceProvider.PersistWorkflowAsync(workflow, result?.Subscriptions, cancellationToken);

                // 7. 入队索引更新
                await QueueProvider.QueueWork(itemId, QueueType.Index);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "执行工作流 {WorkflowId} 时发生错误", itemId);
            throw;
        }
        finally
        {
            // 8. 从灰名单移除（无论工作流状态如何，防止工作流卡在灰名单中）
            Logger.LogDebug("从灰名单移除工作流 {WorkflowId}", itemId);
            _greyList.Remove($"wf:{itemId}");

            // 9. 释放锁
            if (lockAcquired)
            {
                await _lockProvider.ReleaseLock(itemId);
            }

            // 10. 处理后续逻辑
            if (workflow != null && result != null)
            {
                // 处理事件订阅
                foreach (var subscription in result.Subscriptions)
                {
                    await TryProcessSubscription(subscription, cancellationToken);
                }

                // 持久化错误
                if (result.Errors.Count > 0)
                {
                    await _persistenceProvider.PersistErrorsAsync(result.Errors, cancellationToken);
                }

                // 处理延迟执行
                if (workflow.Status == WorkflowStatus.Runnable && workflow.NextExecution.HasValue)
                {
                    var readAheadTicks = _dateTimeProvider.UtcNow.Add(_options.PollInterval).Ticks;
                    
                    if (workflow.NextExecution.Value < readAheadTicks)
                    {
                        // 在轮询间隔内，使用 FutureQueue 延迟重新入队
                        _ = Task.Run(() => FutureQueue(workflow, cancellationToken), cancellationToken);
                    }
                    else
                    {
                        // 超出轮询间隔，使用 ScheduledCommand
                        if (_persistenceProvider.SupportsScheduledCommands)
                        {
                            await _persistenceProvider.ScheduleCommandAsync(new ScheduledCommand
                            {
                                CommandName = ScheduledCommand.ProcessWorkflow,
                                Data = workflow.Id,
                                ExecuteTime = DateTimeOffset.FromUnixTimeMilliseconds(workflow.NextExecution.Value).DateTime
                            }, cancellationToken);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 尝试处理事件订阅
    /// </summary>
    private async Task TryProcessSubscription(EventSubscription subscription, CancellationToken cancellationToken)
    {
        try
        {
            // 跳过活动类型的事件（活动由 ActivityController 处理）
            if (subscription.EventName == Event.EventTypeActivity)
            {
                return;
            }

            // 查找匹配的事件（使用接口方法返回ID列表）
            var eventIds = await _persistenceProvider.GetEvents(
                subscription.EventName,
                subscription.EventKey,
                subscription.SubscribeAsOf,
                cancellationToken);

            foreach (var eventId in eventIds)
            {
                var eventKey = $"evt:{eventId}";
                bool acquiredLock = false;

                try
                {
                    // 尝试获取事件锁
                    acquiredLock = await _lockProvider.AcquireLock(eventKey, cancellationToken);
                    
                    int attempt = 0;
                    while (!acquiredLock && attempt < 10)
                    {
                        await Task.Delay(_options.IdleTime, cancellationToken);
                        acquiredLock = await _lockProvider.AcquireLock(eventKey, cancellationToken);
                        attempt++;
                    }

                    if (!acquiredLock)
                    {
                        Logger.LogWarning("无法获取事件 {EventId} 的锁", eventId);
                        continue;
                    }

                    // 从灰名单移除
                    _greyList.Remove(eventKey);

                    // 标记事件为未处理并重新入队
                    await _persistenceProvider.MarkEventUnprocessed(eventId, cancellationToken);
                    await QueueProvider.QueueWork(eventId, QueueType.Event);
                }
                finally
                {
                    if (acquiredLock)
                    {
                        await _lockProvider.ReleaseLock(eventKey);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "处理事件订阅失败: {EventName} - {EventKey}", 
                subscription.EventName, subscription.EventKey);
        }
    }

    /// <summary>
    /// 延迟队列处理 - 在指定时间后重新入队工作流
    /// </summary>
    private async void FutureQueue(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        try
        {
            if (!workflow.NextExecution.HasValue)
            {
                return;
            }

            // 计算延迟时间
            var target = workflow.NextExecution.Value - _dateTimeProvider.UtcNow.Ticks;
            
            if (target > 0)
            {
                await Task.Delay(TimeSpan.FromTicks(target), cancellationToken);
            }

            // 重新入队工作流
            await QueueProvider.QueueWork(workflow.Id, QueueType.Workflow);
            Logger.LogDebug("工作流 {WorkflowId} 已延迟重新入队", workflow.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "延迟队列处理失败: {WorkflowId}", workflow.Id);
        }
    }

}
