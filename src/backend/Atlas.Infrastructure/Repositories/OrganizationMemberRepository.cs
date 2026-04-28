using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class OrganizationMemberRepository : RepositoryBase<OrganizationMember>
{
    public OrganizationMemberRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<OrganizationMember>> ListByOrganizationAsync(TenantId tenantId, long organizationId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return Db.Queryable<OrganizationMember>()
            .Where(x => x.TenantIdValue == tenantValue && x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationMember?> FindAsync(TenantId tenantId, long organizationId, long userId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return await Db.Queryable<OrganizationMember>()
            .Where(x => x.TenantIdValue == tenantValue && x.OrganizationId == organizationId && x.UserId == userId)
            .FirstAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long organizationId, long userId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return Db.Deleteable<OrganizationMember>()
            .Where(x => x.TenantIdValue == tenantValue && x.OrganizationId == organizationId && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
