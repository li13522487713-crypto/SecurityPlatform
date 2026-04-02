using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Expressions;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Repositories;

public sealed class RuleChainRepository : IRuleChainRepository
{
    private readonly ISqlSugarClient _db;

    public RuleChainRepository(ISqlSugarClient db) => _db = db;

    public async Task<RuleChainDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Queryable<RuleChainDefinition>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken);

    public async Task<PagedResult<RuleChainDefinition>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        TenantId tenantId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var total = new RefAsync<int>();
        var query = _db.Queryable<RuleChainDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) || (x.DisplayName != null && x.DisplayName.Contains(keyword)));
        var items = await query.OrderBy(x => x.SortOrder).OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        return new PagedResult<RuleChainDefinition>(items, total.Value, pageIndex, pageSize);
    }

    public async Task<long> InsertAsync(RuleChainDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(RuleChainDefinition entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.CreatedAt, x.CreatedBy })
            .ExecuteCommandAsync(cancellationToken);

    public async Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Deleteable<RuleChainDefinition>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
}
