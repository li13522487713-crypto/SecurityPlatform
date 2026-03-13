using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Repositories;

public interface IWorkflowMetaRepository
{
    Task<WorkflowMeta?> FindActiveByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<(List<WorkflowMeta> Items, long Total)> GetPagedAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task AddAsync(WorkflowMeta entity, CancellationToken cancellationToken);
    Task UpdateAsync(WorkflowMeta entity, CancellationToken cancellationToken);
}

public interface IWorkflowDraftRepository
{
    Task<WorkflowDraft?> FindByWorkflowIdAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken);
    Task AddAsync(WorkflowDraft entity, CancellationToken cancellationToken);
    Task UpdateAsync(WorkflowDraft entity, CancellationToken cancellationToken);
}

public interface IWorkflowVersionRepository
{
    Task<IReadOnlyList<WorkflowVersion>> ListByWorkflowIdAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken);
    Task<WorkflowVersion?> GetLatestAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken);
    Task AddAsync(WorkflowVersion entity, CancellationToken cancellationToken);
}

public interface IWorkflowExecutionRepository
{
    Task<WorkflowExecution?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task AddAsync(WorkflowExecution entity, CancellationToken cancellationToken);
    Task UpdateAsync(WorkflowExecution entity, CancellationToken cancellationToken);
}

public interface IWorkflowNodeExecutionRepository
{
    Task<IReadOnlyList<WorkflowNodeExecution>> ListByExecutionIdAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken);
    Task<WorkflowNodeExecution?> FindByNodeKeyAsync(TenantId tenantId, long executionId, string nodeKey, CancellationToken cancellationToken);
    Task AddAsync(WorkflowNodeExecution entity, CancellationToken cancellationToken);
    Task BatchAddAsync(IReadOnlyList<WorkflowNodeExecution> entities, CancellationToken cancellationToken);
    Task UpdateAsync(WorkflowNodeExecution entity, CancellationToken cancellationToken);
}
