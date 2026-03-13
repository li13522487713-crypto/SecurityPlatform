using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiWorkspaceRepository : RepositoryBase<AiWorkspace>
{
    public AiWorkspaceRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<AiWorkspace?> FindByUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiWorkspace>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .FirstAsync(cancellationToken);
    }
}
