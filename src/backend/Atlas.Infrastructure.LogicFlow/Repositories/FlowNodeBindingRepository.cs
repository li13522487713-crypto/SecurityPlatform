using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Domain.LogicFlow.Flows;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Repositories;

public sealed class FlowNodeBindingRepository : IFlowNodeBindingRepository
{
    private readonly ISqlSugarClient _db;

    public FlowNodeBindingRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task BulkInsertAsync(IReadOnlyList<FlowNodeBinding> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BulkDeleteAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;
        await _db.Deleteable<FlowNodeBinding>()
            .In(ids.ToList())
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FlowNodeBinding>> GetByFlowIdAsync(
        long flowDefinitionId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<FlowNodeBinding>()
            .Where(x => x.FlowDefinitionId == flowDefinitionId)
            .OrderBy(x => x.SortOrder)
            .OrderBy(x => x.NodeInstanceKey)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task DeleteByFlowIdAsync(long flowDefinitionId, CancellationToken cancellationToken)
    {
        await _db.Deleteable<FlowNodeBinding>()
            .Where(x => x.FlowDefinitionId == flowDefinitionId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
