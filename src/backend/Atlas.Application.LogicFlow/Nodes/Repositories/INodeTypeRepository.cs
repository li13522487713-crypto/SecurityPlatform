using Atlas.Domain.LogicFlow.Nodes;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Nodes.Repositories;

public interface INodeTypeRepository
{
    Task<long> AddAsync(NodeTypeDefinition entity, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(NodeTypeDefinition entity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
    Task<NodeTypeDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<NodeTypeDefinition?> GetByTypeKeyAsync(string typeKey, TenantId tenantId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<NodeTypeDefinition> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        TenantId tenantId,
        string? keyword,
        NodeCategory? category,
        bool? isBuiltIn,
        CancellationToken cancellationToken);
    Task<bool> ExistsByTypeKeyAsync(string typeKey, TenantId tenantId, CancellationToken cancellationToken);
    Task BulkInsertAsync(IReadOnlyList<NodeTypeDefinition> entities, CancellationToken cancellationToken);

    Task<IReadOnlyList<NodeTypeDefinition>> GetManyByTypeKeysAsync(
        IReadOnlyCollection<string> typeKeys,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
