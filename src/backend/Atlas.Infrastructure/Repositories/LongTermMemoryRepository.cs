using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LongTermMemoryRepository : RepositoryBase<LongTermMemory>
{
    public LongTermMemoryRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<LongTermMemory>> ListByUserAgentAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        return await Db.Queryable<LongTermMemory>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.UserId == userId &&
                x.AgentId == agentId)
            .OrderBy(x => x.LastReferencedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<LongTermMemory> Items, long Total)> GetPagedByUserAsync(
        TenantId tenantId,
        long userId,
        long? agentId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<LongTermMemory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId);

        if (agentId.HasValue && agentId.Value > 0)
        {
            query = query.Where(x => x.AgentId == agentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x =>
                x.Content.Contains(normalized) ||
                x.MemoryKey.Contains(normalized) ||
                x.Source.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.LastReferencedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<LongTermMemory>> QueryByKeysAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        IReadOnlyCollection<string> memoryKeys,
        CancellationToken cancellationToken)
    {
        if (memoryKeys.Count == 0)
        {
            return Array.Empty<LongTermMemory>();
        }

        var keyArray = memoryKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return await Db.Queryable<LongTermMemory>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.UserId == userId &&
                x.AgentId == agentId &&
                SqlFunc.ContainsArray(keyArray, x.MemoryKey))
            .ToListAsync(cancellationToken);
    }

    public Task AddRangeAsync(
        IReadOnlyCollection<LongTermMemory> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateRangeAsync(
        IReadOnlyCollection<LongTermMemory> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Updateable(entities.ToList())
            .WhereColumns(x => new { x.Id, x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task TrimToMaxCountAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var safeMax = Math.Max(1, maxCount);
        var allIds = await Db.Queryable<LongTermMemory>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.UserId == userId &&
                x.AgentId == agentId)
            .OrderBy(x => x.LastReferencedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (allIds.Count <= safeMax)
        {
            return;
        }

        var idsToDelete = allIds.Skip(safeMax).ToArray();
        await Db.Deleteable<LongTermMemory>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idsToDelete, x.Id))
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task<int> DeleteByUserAsync(
        TenantId tenantId,
        long userId,
        long memoryId,
        CancellationToken cancellationToken)
    {
        return Db.Deleteable<LongTermMemory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.Id == memoryId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task<int> DeleteByUserAndAgentAsync(
        TenantId tenantId,
        long userId,
        long? agentId,
        CancellationToken cancellationToken)
    {
        var delete = Db.Deleteable<LongTermMemory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId);
        if (agentId.HasValue && agentId.Value > 0)
        {
            delete = delete.Where(x => x.AgentId == agentId.Value);
        }

        return delete.ExecuteCommandAsync(cancellationToken);
    }
}
