using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Nodes.Abstractions;

public interface INodeTypeQueryService
{
    Task<PagedResult<NodeTypeListItem>> QueryAsync(
        NodeTypeQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<NodeTypeDetailResponse?> GetByIdAsync(
        long id,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<NodeTypeDetailResponse?> GetByTypeKeyAsync(
        string typeKey,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NodeCategoryInfo>> GetCategoriesAsync(
        CancellationToken cancellationToken);
}
