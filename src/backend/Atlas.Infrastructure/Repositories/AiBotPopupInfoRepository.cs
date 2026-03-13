using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiBotPopupInfoRepository : RepositoryBase<AiBotPopupInfo>
{
    public AiBotPopupInfoRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<AiBotPopupInfo?> FindByUserAndCodeAsync(
        TenantId tenantId,
        long userId,
        string popupCode,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiBotPopupInfo>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.PopupCode == popupCode)
            .FirstAsync(cancellationToken);
    }
}
