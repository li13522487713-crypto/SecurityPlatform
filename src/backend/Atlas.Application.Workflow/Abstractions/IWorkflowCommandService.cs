using Atlas.Application.Workflow.Models;

namespace Atlas.Application.Workflow.Abstractions;

/// <summary>
/// 工作流命令服务接口
/// </summary>
public interface IWorkflowCommandService
{
    /// <summary>
    /// 启动工作流实例
    /// </summary>
    /// <param name="request">启动请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例ID</returns>
    Task<string> StartWorkflowAsync(StartWorkflowRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 挂起工作流实例
    /// </summary>
    /// <param name="instanceId">实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> SuspendWorkflowAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复工作流实例
    /// </summary>
    /// <param name="instanceId">实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> ResumeWorkflowAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止工作流实例
    /// </summary>
    /// <param name="instanceId">实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> TerminateWorkflowAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布外部事件
    /// </summary>
    /// <param name="request">事件请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishEventAsync(PublishEventRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从 JSON 定义注册动态工作流
    /// </summary>
    /// <param name="request">注册请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RegisterWorkflowFromJsonAsync(RegisterWorkflowDefinitionRequest request, CancellationToken cancellationToken = default);
}
