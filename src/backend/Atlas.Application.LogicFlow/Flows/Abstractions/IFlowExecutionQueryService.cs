using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface IFlowExecutionQueryService
{
    Task<PagedResult<FlowExecutionListItem>> QueryExecutionsAsync(
        long? flowDefId,
        PagedRequest request,
        ExecutionStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<FlowExecutionResponse?> GetExecutionByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NodeRunResponse>> GetNodeRunsAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken);
}
