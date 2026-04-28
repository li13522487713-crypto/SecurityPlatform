using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Coze.Abstractions;

public interface IWorkspaceTaskService
{
    Task<PagedResult<WorkspaceTaskItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        WorkspaceTaskStatus? status,
        string? type,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<WorkspaceTaskDetailDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string taskId,
        CancellationToken cancellationToken);
}

public interface IWorkspaceEvaluationService
{
    Task<PagedResult<EvaluationItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<EvaluationDetailDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string evaluationId,
        CancellationToken cancellationToken);
}

public interface IWorkspaceTestsetService
{
    Task<PagedResult<TestsetItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<string> CreateAsync(
        TenantId tenantId,
        string workspaceId,
        TestsetCreateRequest request,
        CancellationToken cancellationToken);

    Task<TestsetCasePageDto> ListCaseDataAsync(
        TenantId tenantId,
        string workspaceId,
        string? workflowId,
        string? caseName,
        int pageLimit,
        string? nextToken,
        CancellationToken cancellationToken);
}
