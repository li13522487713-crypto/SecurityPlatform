using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ResourceOwnershipTransferRepository : RepositoryBase<ResourceOwnershipTransfer>
{
    public ResourceOwnershipTransferRepository(ISqlSugarClient db) : base(db)
    {
    }

    public Task<List<ResourceOwnershipTransfer>> ListByUserAsync(
        TenantId tenantId,
        long fromUserId,
        CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        return Db.Queryable<ResourceOwnershipTransfer>()
            .Where(x => x.TenantIdValue == tenantValue && x.FromUserId == fromUserId)
            .ToListAsync(cancellationToken);
    }
}
