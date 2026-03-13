using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ConversationRepository : RepositoryBase<Conversation>
{
    public ConversationRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<Conversation> Items, long Total)> GetPagedByAgentAsync(
        TenantId tenantId,
        long agentId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<Conversation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId && x.UserId == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.LastMessageAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<(List<Conversation> Items, long Total)> GetPagedByUserAsync(
        TenantId tenantId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<Conversation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.LastMessageAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }
}
