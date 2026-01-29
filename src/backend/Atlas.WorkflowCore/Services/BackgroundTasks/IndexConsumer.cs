using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.BackgroundTasks;

/// <summary>
/// 索引消费者 - 更新工作流搜索索引
/// </summary>
public class IndexConsumer : QueueConsumer
{
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly ISearchIndex _searchIndex;

    public IndexConsumer(
        IQueueProvider queueProvider,
        IPersistenceProvider persistenceProvider,
        ISearchIndex searchIndex,
        ILogger<IndexConsumer> logger)
        : base(queueProvider, logger)
    {
        _persistenceProvider = persistenceProvider;
        _searchIndex = searchIndex;
    }

    protected override QueueType Queue => QueueType.Index;

    protected override async Task ProcessItem(string itemId, CancellationToken cancellationToken)
    {
        try
        {
            // 1. 获取工作流实例
            var workflow = await _persistenceProvider.GetWorkflowAsync(itemId, cancellationToken);

            if (workflow == null)
            {
                Logger.LogWarning("工作流实例 {WorkflowId} 不存在", itemId);
                return;
            }

            // 2. 索引工作流
            await _searchIndex.IndexWorkflow(workflow);

            Logger.LogDebug("工作流 {WorkflowId} 索引更新完成", itemId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "索引工作流 {WorkflowId} 时发生错误", itemId);
        }
    }
}
