using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Expressions;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Repositories;

public sealed class DecisionTableRepository : IDecisionTableRepository
{
    private readonly ISqlSugarClient _db;

    public DecisionTableRepository(ISqlSugarClient db) => _db = db;

    public async Task<DecisionTableDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Queryable<DecisionTableDefinition>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken);

    public async Task<PagedResult<DecisionTableDefinition>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        TenantId tenantId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var total = new RefAsync<int>();
        var query = _db.Queryable<DecisionTableDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) || (x.DisplayName != null && x.DisplayName.Contains(keyword)));
        var items = await query.OrderBy(x => x.SortOrder).OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        return new PagedResult<DecisionTableDefinition>(items, total.Value, pageIndex, pageSize);
    }

    public async Task<long> InsertAsync(DecisionTableDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(DecisionTableDefinition entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.CreatedAt, x.CreatedBy })
            .ExecuteCommandAsync(cancellationToken);

    public async Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Deleteable<DecisionTableDefinition>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
}
