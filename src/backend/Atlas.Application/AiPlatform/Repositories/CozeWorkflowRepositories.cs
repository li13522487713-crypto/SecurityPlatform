using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Application.AiPlatform.Repositories;

public interface ICozeWorkflowMetaRepository
{
    Task<CozeWorkflowMeta?> FindActiveByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<(List<CozeWorkflowMeta> Items, long Total)> GetPagedAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<(List<CozeWorkflowMeta> Items, long Total)> GetPagedByStatusAsync(
        TenantId tenantId,
        WorkflowLifecycleStatus status,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
    Task AddAsync(CozeWorkflowMeta entity, CancellationToken cancellationToken);
    Task UpdateAsync(CozeWorkflowMeta entity, CancellationToken cancellationToken);
}

public interface ICozeWorkflowDraftRepository
{
    Task<CozeWorkflowDraft?> FindByWorkflowIdAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken);
    Task AddAsync(CozeWorkflowDraft entity, CancellationToken cancellationToken);
    Task UpdateAsync(CozeWorkflowDraft entity, CancellationToken cancellationToken);
}

public interface ICozeWorkflowVersionRepository
{
    Task<IReadOnlyList<CozeWorkflowVersion>> ListByWorkflowIdAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken);
    Task<CozeWorkflowVersion?> GetLatestAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken);
    Task<CozeWorkflowVersion?> FindByIdAsync(TenantId tenantId, long versionId, CancellationToken cancellationToken);
    Task<CozeWorkflowVersion?> FindByWorkflowAndVersionNumberAsync(
        TenantId tenantId,
        long workflowId,
        int versionNumber,
        CancellationToken cancellationToken);
    Task AddAsync(CozeWorkflowVersion entity, CancellationToken cancellationToken);
}
