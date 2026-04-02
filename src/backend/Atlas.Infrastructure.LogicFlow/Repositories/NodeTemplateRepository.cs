using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Nodes;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Repositories;

public sealed class NodeTemplateRepository : INodeTemplateRepository
{
    private readonly ISqlSugarClient _db;

    public NodeTemplateRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(NodeTemplate entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(NodeTemplate entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<NodeTemplate>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<NodeTemplate?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<NodeTemplate>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken);
    }

    public async Task<(IReadOnlyList<NodeTemplate> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        TenantId tenantId,
        string? keyword,
        NodeCategory? category,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<NodeTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<long> AddBlockAsync(BusinessTemplateBlock entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateBlockAsync(BusinessTemplateBlock entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteBlockAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<BusinessTemplateBlock>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<BusinessTemplateBlock?> GetBlockByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BusinessTemplateBlock>()
            .FirstAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<BusinessTemplateBlock> Items, int TotalCount)> QueryBlockPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<BusinessTemplateBlock>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }
}
