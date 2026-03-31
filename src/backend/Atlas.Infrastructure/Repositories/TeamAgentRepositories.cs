using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class TeamAgentRepository : RepositoryBase<TeamAgent>
{
    public TeamAgentRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<TeamAgent> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<TeamAgent>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || (x.Description != null && x.Description.Contains(normalized)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }
}

public sealed class TeamAgentConversationRepository : RepositoryBase<TeamAgentConversation>
{
    public TeamAgentConversationRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<TeamAgentConversation> Items, long Total)> GetPagedByTeamAgentAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<TeamAgentConversation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId && x.UserId == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.LastMessageAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }
}

public sealed class TeamAgentMessageRepository : RepositoryBase<TeamAgentMessage>
{
    public TeamAgentMessageRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentMessage>> GetAllByConversationAsync(
        TenantId tenantId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        return Db.Queryable<TeamAgentMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public Task<int> DeleteByConversationAsync(TenantId tenantId, long conversationId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<TeamAgentMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class TeamAgentExecutionRepository : RepositoryBase<TeamAgentExecution>
{
    public TeamAgentExecutionRepository(ISqlSugarClient db)
        : base(db)
    {
    }
}

public sealed class TeamAgentSchemaDraftRepository : RepositoryBase<TeamAgentSchemaDraft>
{
    public TeamAgentSchemaDraftRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<TeamAgentSchemaDraft?> FindByTeamAgentAndIdAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<TeamAgentSchemaDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId && x.Id == draftId)
            .FirstAsync(cancellationToken);
    }
}
