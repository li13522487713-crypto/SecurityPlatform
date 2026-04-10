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

    Task<WorkflowV2DetailDto?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

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

    Task<WorkflowVersionDiff?> GetVersionDiffAsync(
        TenantId tenantId,
        long workflowId,
        long fromVersionId,
        long toVersionId,
        CancellationToken cancellationToken);
}
