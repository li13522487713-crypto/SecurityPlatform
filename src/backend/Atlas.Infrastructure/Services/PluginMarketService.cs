using Atlas.Application.Plugins.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Plugins;
using Atlas.Domain.Plugins;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class PluginMarketQueryService : IPluginMarketQueryService
{
    private readonly ISqlSugarClient _db;

    public PluginMarketQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<PluginMarketEntry> Items, int Total)> SearchAsync(
        string? keyword, PluginCategory? category, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Queryable<PluginMarketEntry>()
            .Where(e => e.Status == PluginMarketStatus.Published);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(e => e.Name.Contains(keyword) || e.Description.Contains(keyword) || e.Code.Contains(keyword));
        }

        if (category.HasValue)
        {
            query = query.Where(e => e.Category == category.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(e => e.Downloads)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<PluginMarketEntry?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await _db.Queryable<PluginMarketEntry>()
            .Where(e => e.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PluginMarketVersion>> GetVersionsAsync(long entryId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<PluginMarketVersion>()
            .Where(v => v.EntryId == entryId)
            .OrderByDescending(v => v.PublishedAt)
            .ToListAsync(cancellationToken);
    }
}

public sealed class PluginMarketCommandService : IPluginMarketCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public PluginMarketCommandService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<long> PublishAsync(PublishPluginMarketRequest request, Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<PluginMarketEntry>()
            .Where(e => e.Code == request.Code)
            .FirstAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        long entryId;

        if (existing is null)
        {
            entryId = _idGen.Generator.NextId();
            var entry = new PluginMarketEntry
            {
                Id = entryId,
                Code = request.Code,
                Name = request.Name,
                Description = request.Description,
                Author = request.Author,
                Category = request.Category,
                LatestVersion = request.Version,
                IconUrl = request.IconUrl,
                PackageUrl = request.PackageUrl,
                Status = PluginMarketStatus.Published,
                PublishedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                TenantId = tenantId
            };
            await _db.Insertable(entry).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entryId = existing.Id;
            existing.LatestVersion = request.Version;
            existing.Status = PluginMarketStatus.Published;
            existing.PackageUrl = request.PackageUrl;
            existing.UpdatedAt = now;
            await _db.Updateable(existing).Where(e => e.Id == existing.Id).ExecuteCommandAsync(cancellationToken);
        }

        var version = new PluginMarketVersion
        {
            Id = _idGen.Generator.NextId(),
            EntryId = entryId,
            Version = request.Version,
            ReleaseNotes = request.ReleaseNotes,
            PackageUrl = request.PackageUrl,
            PublishedAt = now
        };
        await _db.Insertable(version).ExecuteCommandAsync(cancellationToken);

        return entryId;
    }

    public async Task UpdateAsync(long id, UpdatePluginMarketRequest request, CancellationToken cancellationToken)
    {
        await _db.Updateable<PluginMarketEntry>()
            .SetColumns(e => new PluginMarketEntry
            {
                Name = request.Name,
                Description = request.Description,
                IconUrl = request.IconUrl,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .Where(e => e.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeprecateAsync(long id, CancellationToken cancellationToken)
    {
        await _db.Updateable<PluginMarketEntry>()
            .SetColumns(e => new PluginMarketEntry
            {
                Status = PluginMarketStatus.Deprecated,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .Where(e => e.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task RateAsync(long entryId, Guid tenantId, int rating, CancellationToken cancellationToken)
    {
        if (rating is < 1 or > 5) return;

        var entry = await _db.Queryable<PluginMarketEntry>()
            .Where(e => e.Id == entryId)
            .FirstAsync(cancellationToken);
        if (entry is null) return;

        // 加权平均：新评分加入滚动计算（简化：直接更新均值和计数）
        var newCount = entry.RatingCount + 1;
        var newAvg = Math.Round(
            (entry.AverageRating * entry.RatingCount + rating) / newCount, 1);

        await _db.Updateable<PluginMarketEntry>()
            .SetColumns(e => new PluginMarketEntry
            {
                AverageRating = (decimal)newAvg,
                RatingCount = newCount,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .Where(e => e.Id == entryId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
