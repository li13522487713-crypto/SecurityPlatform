using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class MemberInvitationRepository : RepositoryBase<MemberInvitation>
{
    public MemberInvitationRepository(ISqlSugarClient db) : base(db)
    {
    }

    public Task<List<MemberInvitation>> ListByTenantAsync(TenantId tenantId, long? organizationId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var queryable = Db.Queryable<MemberInvitation>()
            .Where(x => x.TenantIdValue == tenantValue);
        if (organizationId.HasValue)
        {
            var orgId = organizationId.Value;
            queryable = queryable.Where(x => x.OrganizationId == orgId);
        }
        return queryable.OrderBy(x => x.Id, OrderByType.Desc).ToListAsync(cancellationToken);
    }

    public async Task<MemberInvitation?> FindByTokenAsync(TenantId tenantId, string token, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var normalized = token.Trim();
        return await Db.Queryable<MemberInvitation>()
            .Where(x => x.TenantIdValue == tenantValue && x.Token == normalized)
            .FirstAsync(cancellationToken);
    }
}
