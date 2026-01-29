using Atlas.Application.Workflow.Models;
using Atlas.Core.Models;

namespace Atlas.Application.Workflow.Abstractions;

/// <summary>
/// 工作流查询服务接口
/// </summary>
public interface IWorkflowQueryService
{
    /// <summary>
    /// 获取工作流实例详情
    /// </summary>
    /// <param name="instanceId">实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流实例详情</returns>
    Task<WorkflowInstanceResponse?> GetWorkflowInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询工作流实例列表
    /// </summary>
    /// <param name="request">分页请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<WorkflowInstanceListItem>> GetWorkflowInstancesAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有已注册的工作流定义
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流定义列表</returns>
    Task<IEnumerable<WorkflowDefinitionResponse>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定工作流定义
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="version">版本号（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作流定义</returns>
    Task<WorkflowDefinitionResponse?> GetDefinitionAsync(string workflowId, int? version = null, CancellationToken cancellationToken = default);
}
