using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Nodes.Abstractions;

public interface INodeTemplateQueryService
{
    Task<PagedResult<NodeTemplateListItem>> QueryAsync(
        NodeTemplateQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<NodeTemplateDetailResponse?> GetByIdAsync(
        long id,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
