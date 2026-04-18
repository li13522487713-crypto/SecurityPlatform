using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class TenantNetworkPolicyRepository : RepositoryBase<TenantNetworkPolicy>
{
    public TenantNetworkPolicyRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<TenantNetworkPolicy?> FindByTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return await Db.Queryable<TenantNetworkPolicy>()
            .Where(x => x.TenantIdValue == tenantValue)
            .FirstAsync(cancellationToken);
    }
}

public sealed class TenantDataResidencyPolicyRepository : RepositoryBase<TenantDataResidencyPolicy>
{
    public TenantDataResidencyPolicyRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<TenantDataResidencyPolicy?> FindByTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return await Db.Queryable<TenantDataResidencyPolicy>()
            .Where(x => x.TenantIdValue == tenantValue)
            .FirstAsync(cancellationToken);
    }
}
