using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class TenantIdentityProviderRepository : RepositoryBase<TenantIdentityProvider>
{
    public TenantIdentityProviderRepository(ISqlSugarClient db) : base(db)
    {
    }

    public Task<List<TenantIdentityProvider>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return Db.Queryable<TenantIdentityProvider>()
            .Where(x => x.TenantIdValue == tenantValue)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantIdentityProvider?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var normalized = code.Trim();
        return await Db.Queryable<TenantIdentityProvider>()
            .Where(x => x.TenantIdValue == tenantValue && x.Code == normalized)
            .FirstAsync(cancellationToken);
    }
}
