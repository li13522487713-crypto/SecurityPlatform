using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Domain.LogicFlow.Flows;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Repositories;

public sealed class LogicFlowRepository : ILogicFlowRepository
{
    private readonly ISqlSugarClient _db;

    public LogicFlowRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(LogicFlowDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(LogicFlowDefinition entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<LogicFlowDefinition>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<LogicFlowDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<LogicFlowDefinition>()
            .FirstAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<LogicFlowDefinition> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        FlowStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<LogicFlowDefinition>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.DisplayName.Contains(keyword));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _db.Queryable<LogicFlowDefinition>()
            .AnyAsync(x => x.Name == name, cancellationToken);
    }
}
