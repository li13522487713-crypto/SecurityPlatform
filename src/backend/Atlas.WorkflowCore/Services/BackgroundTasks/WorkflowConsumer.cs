using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.BackgroundTasks;

/// <summary>
/// 工作流消费者 - 从队列消费工作流实例并执行
/// </summary>
public class WorkflowConsumer : QueueConsumer
{
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly IWorkflowExecutor _executor;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILifeCycleEventPublisher _eventPublisher;

    public WorkflowConsumer(
        IQueueProvider queueProvider,
        IPersistenceProvider persistenceProvider,
        IWorkflowExecutor executor,
        IDistributedLockProvider lockProvider,
        ILifeCycleEventPublisher eventPublisher,
        ILogger<WorkflowConsumer> logger)
        : base(queueProvider, logger)
    {
        _persistenceProvider = persistenceProvider;
        _executor = executor;
        _lockProvider = lockProvider;
        _eventPublisher = eventPublisher;
    }

    protected override QueueType Queue => QueueType.Workflow;

    protected override async Task ProcessItem(string itemId, CancellationToken cancellationToken)
    {
        var lockAcquired = false;

        try
        {
            // 1. 获取分布式锁
            lockAcquired = await _lockProvider.AcquireLock(itemId, cancellationToken);

            if (!lockAcquired)
            {
                Logger.LogDebug("无法获取工作流 {WorkflowId} 的锁，跳过此次执行", itemId);
                // 重新入队，稍后重试
                await QueueProvider.QueueWork(itemId, QueueType.Workflow);
                return;
            }

            // 2. 获取工作流实例
            var instance = await _persistenceProvider.GetWorkflowAsync(itemId, cancellationToken);

            if (instance == null)
            {
                Logger.LogWarning("工作流实例 {WorkflowId} 不存在", itemId);
                return;
            }

            if (instance.Status != WorkflowStatus.Runnable)
            {
                Logger.LogDebug("工作流 {WorkflowId} 状态为 {Status}，不可执行", itemId, instance.Status);
                return;
            }

            // 3. 执行工作流
            Logger.LogDebug("开始执行工作流 {WorkflowId}", itemId);
            await _executor.Execute(instance, cancellationToken);

            // 4. 持久化工作流状态
            await _persistenceProvider.PersistWorkflowAsync(instance, cancellationToken);

            // 5. 如果工作流仍可运行，重新入队
            if (instance.Status == WorkflowStatus.Runnable)
            {
                await QueueProvider.QueueWork(itemId, QueueType.Workflow);
                Logger.LogDebug("工作流 {WorkflowId} 重新入队", itemId);
            }
            else
            {
                Logger.LogInformation("工作流 {WorkflowId} 执行完成，状态: {Status}", itemId, instance.Status);
            }

            // 6. 入队索引更新
            await QueueProvider.QueueWork(itemId, QueueType.Index);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "执行工作流 {WorkflowId} 时发生错误", itemId);

            // 重新入队，稍后重试
            await QueueProvider.QueueWork(itemId, QueueType.Workflow);
        }
        finally
        {
            // 7. 释放锁
            if (lockAcquired)
            {
                await _lockProvider.ReleaseLock(itemId);
            }
        }
    }
}
