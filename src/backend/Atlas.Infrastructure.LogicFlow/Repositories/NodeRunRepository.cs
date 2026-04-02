using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Domain.LogicFlow.Flows;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Repositories;

public sealed class NodeRunRepository : INodeRunRepository
{
    private readonly ISqlSugarClient _db;

    public NodeRunRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(NodeRun entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task BulkInsertAsync(IReadOnlyList<NodeRun> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(NodeRun entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<NodeRun?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<NodeRun>()
            .FirstAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<NodeRun>> GetByExecutionIdAsync(long flowExecutionId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<NodeRun>()
            .Where(x => x.FlowExecutionId == flowExecutionId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<NodeRun?> GetByExecutionIdAndNodeKeyAsync(
        long flowExecutionId,
        string nodeKey,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<NodeRun>()
            .FirstAsync(x => x.FlowExecutionId == flowExecutionId && x.NodeKey == nodeKey, cancellationToken);
    }
}
