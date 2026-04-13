using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiAppConversationTemplateRepository : RepositoryBase<AiAppConversationTemplate>
{
    public AiAppConversationTemplateRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AiAppConversationTemplate>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiAppConversationTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.IsDefault, OrderByType.Desc)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiAppConversationTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
