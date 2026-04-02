using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface ILogicFlowQueryService
{
    Task<PagedResult<LogicFlowListItem>> QueryAsync(
        PagedRequest request,
        FlowStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<LogicFlowDetailResponse?> GetByIdAsync(
        long id,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
