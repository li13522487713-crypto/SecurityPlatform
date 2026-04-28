using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long creatorId,
        AgentCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(TenantId tenantId, long id, AgentUpdateRequest request, CancellationToken cancellationToken);

    Task<WorkflowBindingDto> BindWorkflowAsync(
        TenantId tenantId,
        long id,
        long? workflowId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentDatabaseBindingItem>> BindDatabaseAsync(
        TenantId tenantId,
        long id,
        AgentDatabaseBindingInput request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentDatabaseBindingItem>> UnbindDatabaseAsync(
        TenantId tenantId,
        long id,
        long databaseId,
        CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<long> DuplicateAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken);

    Task PublishAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
