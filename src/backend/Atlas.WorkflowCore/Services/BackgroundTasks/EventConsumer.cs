using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.BackgroundTasks;

/// <summary>
/// 事件消费者 - 处理事件并唤醒等待的工作流
/// </summary>
public class EventConsumer : QueueConsumer
{
    private readonly IPersistenceProvider _persistenceProvider;

    public EventConsumer(
        IQueueProvider queueProvider,
        IPersistenceProvider persistenceProvider,
        ILogger<EventConsumer> logger)
        : base(queueProvider, logger)
    {
        _persistenceProvider = persistenceProvider;
    }

    protected override QueueType Queue => QueueType.Event;

    protected override async Task ProcessItem(string itemId, CancellationToken cancellationToken)
    {
        try
        {
            // 1. 获取事件
            var evt = await _persistenceProvider.GetEventAsync(itemId, cancellationToken);

            if (evt == null)
            {
                Logger.LogWarning("事件 {EventId} 不存在", itemId);
                return;
            }

            if (evt.IsProcessed)
            {
                Logger.LogDebug("事件 {EventId} 已处理", itemId);
                return;
            }

            Logger.LogDebug("处理事件 {EventId}: {EventName} - {EventKey}", itemId, evt.EventName, evt.EventKey);

            // 2. 获取事件订阅
            var subscriptions = await _persistenceProvider.GetEventSubscriptionsAsync(
                evt.EventName,
                evt.EventKey,
                evt.EventTime,
                cancellationToken);

            var subscriptionList = subscriptions.ToList();

            if (subscriptionList.Count == 0)
            {
                Logger.LogDebug("事件 {EventId} 没有订阅者", itemId);
                await _persistenceProvider.MarkEventProcessedAsync(itemId, cancellationToken);
                return;
            }

            Logger.LogDebug("事件 {EventId} 有 {Count} 个订阅者", itemId, subscriptionList.Count);

            // 3. 唤醒等待的工作流
            foreach (var subscription in subscriptionList)
            {
                try
                {
                    // 获取工作流实例
                    var workflow = await _persistenceProvider.GetWorkflowAsync(subscription.WorkflowId, cancellationToken);

                    if (workflow == null)
                    {
                        Logger.LogWarning("订阅 {SubscriptionId} 的工作流 {WorkflowId} 不存在",
                            subscription.Id, subscription.WorkflowId);
                        continue;
                    }

                    // 查找等待事件的执行指针
                    var pointer = workflow.ExecutionPointers.FirstOrDefault(p =>
                        p.Id == subscription.ExecutionPointerId &&
                        p.Status == PointerStatus.WaitingForEvent);

                    if (pointer == null)
                    {
                        Logger.LogDebug("订阅 {SubscriptionId} 的执行指针 {PointerId} 不存在或状态不正确",
                            subscription.Id, subscription.ExecutionPointerId);
                        continue;
                    }

                    // 唤醒执行指针
                    pointer.Active = true;
                    pointer.Status = PointerStatus.Pending;
                    pointer.EventData = evt.EventData?.ToString();

                    // 更新工作流状态
                    if (workflow.Status != WorkflowStatus.Runnable)
                    {
                        workflow.Status = WorkflowStatus.Runnable;
                    }

                    // 持久化工作流
                    await _persistenceProvider.PersistWorkflowAsync(workflow, cancellationToken);

                    // 将工作流入队执行
                    await QueueProvider.QueueWork(workflow.Id, QueueType.Workflow);

                    Logger.LogDebug("工作流 {WorkflowId} 已被事件 {EventId} 唤醒",
                        workflow.Id, itemId);

                    // 终止订阅
                    await _persistenceProvider.TerminateEventSubscriptionAsync(subscription.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "处理订阅 {SubscriptionId} 时发生错误", subscription.Id);
                }
            }

            // 4. 标记事件为已处理
            await _persistenceProvider.MarkEventProcessedAsync(itemId, cancellationToken);

            Logger.LogInformation("事件 {EventId} 处理完成", itemId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "处理事件 {EventId} 时发生错误", itemId);

            // 重新入队，稍后重试
            await QueueProvider.QueueWork(itemId, QueueType.Event);
        }
    }
}
