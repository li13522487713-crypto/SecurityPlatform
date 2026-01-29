namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 工作流控制器接口 - 提供工作流实例的控制操作
/// </summary>
public interface IWorkflowController
{
    /// <summary>
    /// 启动一个新的工作流实例
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="version">版本（null表示使用最新版本）</param>
    /// <param name="data">工作流数据</param>
    /// <param name="reference">引用标识（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例ID</returns>
    Task<string> StartWorkflowAsync(string workflowId, int? version, object? data, string? reference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动一个新的工作流实例（泛型版本）
    /// </summary>
    /// <typeparam name="TData">工作流数据类型</typeparam>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="version">版本（null表示使用最新版本）</param>
    /// <param name="data">工作流数据</param>
    /// <param name="reference">引用标识（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例ID</returns>
    Task<string> StartWorkflowAsync<TData>(string workflowId, int? version, TData? data, string? reference = null, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// 挂起工作流实例
    /// </summary>
    /// <param name="workflowInstanceId">工作流实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> SuspendWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复工作流实例
    /// </summary>
    /// <param name="workflowInstanceId">工作流实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ResumeWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止工作流实例
    /// </summary>
    /// <param name="workflowInstanceId">工作流实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> TerminateWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布外部事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventKey">事件键</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="effectiveDate">生效日期（可选，默认为当前时间）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishEventAsync(string eventName, string eventKey, object? eventData = null, DateTime? effectiveDate = null, CancellationToken cancellationToken = default);
}
