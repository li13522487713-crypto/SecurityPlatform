using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiVariableRepository : RepositoryBase<AiVariable>
{
    public AiVariableRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<AiVariable> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        AiVariableScope? scope,
        long? scopeId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiVariable>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Key.Contains(normalized));
        }

        if (scope.HasValue)
        {
            query = query.Where(x => x.Scope == scope.Value);
        }

        if (scopeId.HasValue && scopeId.Value > 0)
        {
            query = query.Where(x => x.ScopeId == scopeId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<bool> ExistsByKeyAsync(
        TenantId tenantId,
        string key,
        AiVariableScope scope,
        long? scopeId,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiVariable>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.Key == key &&
                x.Scope == scope &&
                x.ScopeId == scopeId);

        if (excludeId.HasValue && excludeId.Value > 0)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.CountAsync(cancellationToken) > 0;
    }
}
