using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ICozeWorkflowQueryService
{
    Task<PagedResult<CozeWorkflowListItem>> ListAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<PagedResult<CozeWorkflowListItem>> ListPublishedAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<CozeWorkflowDetailDto?> GetAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken,
        string? source = null,
        long? versionId = null);

    Task<IReadOnlyList<CozeWorkflowVersionDto>> ListVersionsAsync(
        TenantId tenantId, long workflowId, CancellationToken cancellationToken);

    Task<CozeWorkflowExecutionDto?> GetExecutionProcessAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DagWorkflowNodeTypeDto>> GetNodeTypesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DagWorkflowNodeTemplateDto>> GetNodeTemplatesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DagWorkflowNodeTemplateDto>> SearchNodeTemplatesAsync(
        string? keyword,
        IReadOnlyList<string>? categories,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CozeWorkflowHistorySchemaDto?> GetHistorySchemaAsync(
        TenantId tenantId,
        long workflowId,
        string? commitId,
        long? executionId,
        CancellationToken cancellationToken);

    Task<WorkflowNodeExecutionHistoryDto?> GetNodeExecuteHistoryAsync(
        TenantId tenantId,
        long workflowId,
        long? executionId,
        string nodeKey,
        CancellationToken cancellationToken);

    Task<DagWorkflowRunTraceDto?> GetRunTraceAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken);

    Task<CozeWorkflowReferenceDto?> GetDependenciesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken);
}
