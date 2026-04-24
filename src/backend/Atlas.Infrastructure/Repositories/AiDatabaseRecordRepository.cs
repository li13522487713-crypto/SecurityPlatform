using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiDatabaseRecordRepository : RepositoryBase<AiDatabaseRecord>
{
    public AiDatabaseRecordRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<AiDatabaseRecord> Items, long Total)> GetPagedByDatabaseAsync(
        TenantId tenantId,
        long databaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        string? channelId = null)
    {
        var query = Db.Queryable<AiDatabaseRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId);
        query = ApplyMetadataFilters(query, ownerUserId, channelId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<int> CountByDatabaseAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        string? channelId = null)
    {
        var query = Db.Queryable<AiDatabaseRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId);
        query = ApplyMetadataFilters(query, ownerUserId, channelId);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<AiDatabaseRecord?> FindByDatabaseAndIdAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        string? channelId = null)
    {
        var query = Db.Queryable<AiDatabaseRecord>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.DatabaseId == databaseId &&
                x.Id == recordId);
        query = ApplyMetadataFilters(query, ownerUserId, channelId);
        return await query.FirstAsync(cancellationToken);
    }

    /// <summary>D1：旧记录默认 NULL 视为 MultiUser 兼容（不强制 owner/channel 过滤）。
    /// 调用方传入非空时，匹配等值或保留 NULL（兼容旧数据）。</summary>
    private static ISugarQueryable<AiDatabaseRecord> ApplyMetadataFilters(
        ISugarQueryable<AiDatabaseRecord> query,
        long? ownerUserId,
        string? channelId)
    {
        if (ownerUserId.HasValue)
        {
            var ownerVal = ownerUserId.Value;
            query = query.Where(x => x.OwnerUserId == 0 || x.OwnerUserId == ownerVal);
        }
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            var channelVal = channelId.Trim();
            query = query.Where(x => x.ChannelId == string.Empty || x.ChannelId == channelVal);
        }
        return query;
    }

    public Task DeleteByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiDatabaseRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyCollection<AiDatabaseRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(records.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
