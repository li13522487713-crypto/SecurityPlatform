using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Nodes;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Repositories;

public sealed class NodeTypeRepository : INodeTypeRepository
{
    private readonly ISqlSugarClient _db;

    public NodeTypeRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(NodeTypeDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(NodeTypeDefinition entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<NodeTypeDefinition>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<NodeTypeDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<NodeTypeDefinition>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken);
    }

    public async Task<NodeTypeDefinition?> GetByTypeKeyAsync(string typeKey, TenantId tenantId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<NodeTypeDefinition>()
            .FirstAsync(x => x.TypeKey == typeKey && x.TenantIdValue == tenantId.Value, cancellationToken);
    }

    public async Task<(IReadOnlyList<NodeTypeDefinition> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        TenantId tenantId,
        string? keyword,
        NodeCategory? category,
        bool? isBuiltIn,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<NodeTypeDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.DisplayName.Contains(keyword) || x.TypeKey.Contains(keyword));
        }

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        if (isBuiltIn.HasValue)
        {
            query = query.Where(x => x.IsBuiltIn == isBuiltIn.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Category)
            .OrderBy(x => x.TypeKey)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<bool> ExistsByTypeKeyAsync(string typeKey, TenantId tenantId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<NodeTypeDefinition>()
            .AnyAsync(x => x.TypeKey == typeKey && x.TenantIdValue == tenantId.Value, cancellationToken);
    }

    public async Task BulkInsertAsync(IReadOnlyList<NodeTypeDefinition> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NodeTypeDefinition>> GetManyByTypeKeysAsync(
        IReadOnlyCollection<string> typeKeys,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        if (typeKeys.Count == 0)
            return Array.Empty<NodeTypeDefinition>();

        var distinct = typeKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return await _db.Queryable<NodeTypeDefinition>()
            .Where(x => distinct.Contains(x.TypeKey) && x.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);
    }
}
