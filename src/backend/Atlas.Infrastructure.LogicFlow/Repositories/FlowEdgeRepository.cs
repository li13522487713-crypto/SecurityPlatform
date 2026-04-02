using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Domain.LogicFlow.Flows;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Repositories;

public sealed class FlowEdgeRepository : IFlowEdgeRepository
{
    private readonly ISqlSugarClient _db;

    public FlowEdgeRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task BulkInsertAsync(IReadOnlyList<FlowEdgeDefinition> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BulkDeleteAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;
        await _db.Deleteable<FlowEdgeDefinition>()
            .In(ids.ToList())
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FlowEdgeDefinition>> GetByFlowIdAsync(
        long flowDefinitionId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<FlowEdgeDefinition>()
            .Where(x => x.FlowDefinitionId == flowDefinitionId)
            .OrderBy(x => x.Priority)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task DeleteByFlowIdAsync(long flowDefinitionId, CancellationToken cancellationToken)
    {
        await _db.Deleteable<FlowEdgeDefinition>()
            .Where(x => x.FlowDefinitionId == flowDefinitionId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
