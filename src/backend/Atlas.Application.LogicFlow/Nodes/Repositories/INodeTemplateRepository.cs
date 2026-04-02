using Atlas.Domain.LogicFlow.Nodes;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Nodes.Repositories;

public interface INodeTemplateRepository
{
    Task<long> AddAsync(NodeTemplate entity, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(NodeTemplate entity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
    Task<NodeTemplate?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<NodeTemplate> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        TenantId tenantId,
        string? keyword,
        NodeCategory? category,
        CancellationToken cancellationToken);

    Task<long> AddBlockAsync(BusinessTemplateBlock entity, CancellationToken cancellationToken);
    Task<bool> UpdateBlockAsync(BusinessTemplateBlock entity, CancellationToken cancellationToken);
    Task<bool> DeleteBlockAsync(long id, CancellationToken cancellationToken);
    Task<BusinessTemplateBlock?> GetBlockByIdAsync(long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<BusinessTemplateBlock> Items, int TotalCount)> QueryBlockPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
}
