using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Domain.LogicFlow.Flows;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Repositories;

public sealed class FlowExecutionRepository : IFlowExecutionRepository
{
    private readonly ISqlSugarClient _db;

    public FlowExecutionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(FlowExecution entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(FlowExecution entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<FlowExecution?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<FlowExecution>()
            .FirstAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<FlowExecution> Items, int TotalCount)> QueryPageAsync(
        Guid tenantId,
        long? flowDefinitionId,
        ExecutionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<FlowExecution>().Where(x => x.TenantIdValue == tenantId);
        if (flowDefinitionId.HasValue)
            query = query.Where(x => x.FlowDefinitionId == flowDefinitionId.Value);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (list, totalCount);
    }

    public async Task<FlowExecution?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return null;
        return await _db.Queryable<FlowExecution>()
            .FirstAsync(x => x.CorrelationId == correlationId, cancellationToken);
    }
}
