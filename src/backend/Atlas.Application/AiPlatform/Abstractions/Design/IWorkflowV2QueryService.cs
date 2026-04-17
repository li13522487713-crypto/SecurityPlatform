using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// V2 工作流查询服务（列表/详情/版本/执行进度/节点详情/节点类型）。
/// </summary>
public interface IWorkflowV2QueryService
{
    Task<PagedResult<WorkflowV2ListItem>> ListAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<PagedResult<WorkflowV2ListItem>> ListPublishedAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<WorkflowV2DetailDto?> GetAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken,
        string? source = null,
        long? versionId = null);

    Task<IReadOnlyList<WorkflowV2VersionDto>> ListVersionsAsync(
        TenantId tenantId, long workflowId, CancellationToken cancellationToken);

    Task<WorkflowV2ExecutionDto?> GetExecutionProcessAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken);

    Task<WorkflowV2ExecutionCheckpointDto?> GetExecutionCheckpointAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken);

    Task<WorkflowV2ExecutionDebugViewDto?> GetExecutionDebugViewAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken);

    Task<WorkflowV2NodeExecutionDto?> GetNodeExecutionDetailAsync(
        TenantId tenantId, long executionId, string nodeKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowV2NodeTypeDto>> GetNodeTypesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowV2NodeTemplateDto>> GetNodeTemplatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 节点面板搜索：用于编辑器节点面板的关键字过滤（名称、描述、分类、Key 模糊匹配），
    /// 与上游 Coze /api/workflow_api/node_panel_search 行为对齐。
    /// </summary>
    Task<IReadOnlyList<WorkflowV2NodeTemplateDto>> SearchNodeTemplatesAsync(
        string? keyword,
        IReadOnlyList<string>? categories,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定工作流在某个节点 Key 之上的可见变量树：
    /// - 全局变量（CanvasSchema.Globals）
    /// - 上游节点的输出端口与字段
    /// - 系统变量（来自 IAiVariableService.GetSystemVariableDefinitionsAsync）
    /// 当 <paramref name="nodeKey"/> 为空时返回所有节点输出 + 全局/系统变量。
    /// </summary>
    Task<WorkflowVariableTreeDto> GetVariableTreeAsync(
        TenantId tenantId,
        long workflowId,
        string? nodeKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// 按节点查询执行历史快照（输入/输出/上下文变量），用于调试面板与单节点回看。
    /// </summary>
    Task<WorkflowNodeExecutionHistoryDto?> GetNodeExecuteHistoryAsync(
        TenantId tenantId,
        long workflowId,
        long? executionId,
        string nodeKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// 按 commitId（版本号字符串）拉取工作流历史 schema，用于「历史版本回看」与试运行结果回放。
    /// </summary>
    Task<WorkflowHistorySchemaDto?> GetHistorySchemaAsync(
        TenantId tenantId,
        long workflowId,
        string? commitId,
        long? executionId,
        CancellationToken cancellationToken);

    Task<WorkflowVersionDiff?> GetVersionDiffAsync(
        TenantId tenantId,
        long workflowId,
        long fromVersionId,
        long toVersionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取执行实例的结构化 Trace（步骤列表 + 边状态），用于前端时间线回放。
    /// </summary>
    Task<WorkflowV2RunTraceDto?> GetRunTraceAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken);

    Task<WorkflowV2DependencyDto?> GetDependenciesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken);
}
