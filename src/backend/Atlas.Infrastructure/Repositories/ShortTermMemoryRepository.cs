using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Repositories;

public sealed class ShortTermMemoryRepository : RepositoryBase<ShortTermMemory>
{
    public ShortTermMemoryRepository(SqlSugar.ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<ShortTermMemory?> FindByConversationAsync(
        TenantId tenantId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<ShortTermMemory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .FirstAsync(cancellationToken);
    }
}
