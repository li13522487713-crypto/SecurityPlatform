using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class OidcLinkRepository : RepositoryBase<OidcLink>, IOidcLinkRepository
{
    public OidcLinkRepository(ISqlSugarClient db) : base(db) { }

    public async Task<OidcLink?> FindByProviderSubAsync(TenantId tenantId, string providerId, string externalSub, CancellationToken cancellationToken)
    {
        return await Db.Queryable<OidcLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.ExternalSub == externalSub)
            .FirstAsync(cancellationToken);
    }

    public async Task<OidcLink?> FindByEmailAsync(TenantId tenantId, string providerId, string email, CancellationToken cancellationToken)
    {
        return await Db.Queryable<OidcLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.Email == email)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OidcLink>> GetByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        var list = await Db.Queryable<OidcLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ToListAsync(cancellationToken);
        return list;
    }
}
