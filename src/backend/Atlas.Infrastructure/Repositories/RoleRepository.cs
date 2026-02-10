using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class RoleRepository : RepositoryBase<Role>, IRoleRepository
{
    public RoleRepository(ISqlSugarClient db) : base(db) { }

    public async Task<Role?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        return await Db.Queryable<Role>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> QueryByCodesAsync(
        TenantId tenantId,
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return Array.Empty<Role>();
        }

        var distinctCodes = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctCodes.Length == 0)
        {
            return Array.Empty<Role>();
        }

        return await Db.Queryable<Role>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(distinctCodes, x.Code))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Role> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        bool? isSystem,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<Role>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }
        if (isSystem.HasValue)
        {
            query = query.Where(x => x.IsSystem == isSystem.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public Task AddRangeAsync(IReadOnlyList<Role> roles, CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(roles.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
