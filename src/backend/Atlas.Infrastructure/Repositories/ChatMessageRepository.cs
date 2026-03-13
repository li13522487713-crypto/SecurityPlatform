using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ChatMessageRepository : RepositoryBase<ChatMessage>
{
    public ChatMessageRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<ChatMessage>> GetByConversationAsync(
        TenantId tenantId,
        long conversationId,
        bool afterContextClear,
        int? limit,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<ChatMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .Where(x => !x.IsContextCleared);

        if (afterContextClear)
        {
            var boundary = await Db.Queryable<ChatMessage>()
                .Where(x =>
                    x.TenantIdValue == tenantId.Value &&
                    x.ConversationId == conversationId &&
                    x.IsContextCleared)
                .OrderBy(x => x.CreatedAt, OrderByType.Desc)
                .OrderBy(x => x.Id, OrderByType.Desc)
                .FirstAsync(cancellationToken);

            if (boundary is not null)
            {
                query = query.Where(x => x.CreatedAt > boundary.CreatedAt || (x.CreatedAt == boundary.CreatedAt && x.Id > boundary.Id));
            }
        }

        if (limit.HasValue && limit.Value > 0)
        {
            var latest = await query
                .OrderBy(x => x.CreatedAt, OrderByType.Desc)
                .OrderBy(x => x.Id, OrderByType.Desc)
                .Take(limit.Value)
                .ToListAsync(cancellationToken);

            return latest
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToList();
        }

        return await query
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetAllByConversationAsync(
        TenantId tenantId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<ChatMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChatMessage?> FindByConversationAndIdAsync(
        TenantId tenantId,
        long conversationId,
        long messageId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<ChatMessage>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.ConversationId == conversationId &&
                x.Id == messageId)
            .FirstAsync(cancellationToken);
    }

    public Task DeleteByConversationAsync(TenantId tenantId, long conversationId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<ChatMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
