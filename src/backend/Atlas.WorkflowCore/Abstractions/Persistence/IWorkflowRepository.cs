using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions.Persistence;

/// <summary>
/// 工作流实例仓储接口
/// </summary>
public interface IWorkflowRepository
{
    /// <summary>
    /// 创建新的工作流实例
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例ID</returns>
    Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化工作流实例
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化工作流实例（带事件订阅）
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="subscriptions">事件订阅列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可运行的工作流实例ID列表
    /// </summary>
    /// <param name="asAt">时间点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例ID列表</returns>
    Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取工作流实例
    /// </summary>
    /// <param name="id">工作流实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例</returns>
    Task<WorkflowInstance> GetWorkflowInstance(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取工作流实例
    /// </summary>
    /// <param name="ids">工作流实例ID列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例列表</returns>
    Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken = default);
}
