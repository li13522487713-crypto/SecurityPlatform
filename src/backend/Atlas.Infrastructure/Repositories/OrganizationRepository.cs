using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class OrganizationRepository : RepositoryBase<Organization>
{
    public OrganizationRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<Organization>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return Db.Queryable<Organization>()
            .Where(x => x.TenantIdValue == tenantValue)
            .OrderBy(x => x.IsDefault, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Organization?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var normalized = code.Trim();
        return await Db.Queryable<Organization>()
            .Where(x => x.TenantIdValue == tenantValue && x.Code == normalized)
            .FirstAsync(cancellationToken);
    }

    public async Task<Organization?> FindDefaultAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return await Db.Queryable<Organization>()
            .Where(x => x.TenantIdValue == tenantValue && x.IsDefault)
            .FirstAsync(cancellationToken);
    }
}
