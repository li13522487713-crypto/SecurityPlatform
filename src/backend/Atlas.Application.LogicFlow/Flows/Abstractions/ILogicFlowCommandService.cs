using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface ILogicFlowCommandService
{
    Task<long> CreateAsync(
        LogicFlowCreateRequest request,
        IReadOnlyList<FlowNodeBindingRequest> nodes,
        IReadOnlyList<FlowEdgeRequest> edges,
        TenantId tenantId,
        string userId,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        long id,
        LogicFlowUpdateRequest request,
        IReadOnlyList<FlowNodeBindingRequest> nodes,
        IReadOnlyList<FlowEdgeRequest> edges,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task PublishAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task ArchiveAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}
