using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

/// <summary>
/// 页面仓储实现（M01）。批量排序通过单条 UpdateColumns IN 操作，避免循环 DB（AGENTS.md 强约束）。
/// </summary>
public sealed class PageDefinitionRepository : IPageDefinitionRepository
{
    private readonly ISqlSugarClient _db;

    public PageDefinitionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> InsertAsync(PageDefinition page, CancellationToken cancellationToken)
    {
        await _db.Insertable(page).ExecuteCommandAsync(cancellationToken);
        return page.Id;
    }

    public async Task<bool> UpdateAsync(PageDefinition page, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(page)
            .Where(x => x.Id == page.Id && x.AppId == page.AppId && x.TenantIdValue == page.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<int> ReorderBatchAsync(TenantId tenantId, long appId, IReadOnlyDictionary<long, int> idToOrder, CancellationToken cancellationToken)
    {
        if (idToOrder.Count == 0) return 0;

        // 一次性拉取受影响页面 → 内存修改 OrderNo → 批量 UpdateColumns 单条 SQL，避免循环内 DB 操作。
        var ids = idToOrder.Keys.ToArray();
        var pages = await _db.Queryable<PageDefinition>()
            .Where(p => p.TenantIdValue == tenantId.Value && p.AppId == appId)
            .Where(p => SqlFunc.ContainsArray(ids, p.Id))
            .ToListAsync(cancellationToken);

        if (pages.Count == 0) return 0;

        foreach (var p in pages)
        {
            if (idToOrder.TryGetValue(p.Id, out var newOrder))
            {
                p.Reorder(newOrder);
            }
        }

        var rows = await _db.Updateable(pages)
            .UpdateColumns(it => new { it.OrderNo, it.UpdatedAt })
            .ExecuteCommandAsync(cancellationToken);
        return rows;
    }

    public async Task<bool> DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<PageDefinition>()
            .Where(x => x.Id == id && x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public Task<PageDefinition?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken)
    {
        return _db.Queryable<PageDefinition>()
            .Where(x => x.Id == id && x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<bool> ExistsCodeAsync(TenantId tenantId, long appId, string code, long? excludeId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<PageDefinition>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value && x.Code == code);
        if (excludeId.HasValue)
        {
            q = q.Where(x => x.Id != excludeId.Value);
        }
        return await q.AnyAsync();
    }

    public async Task<IReadOnlyList<PageDefinition>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<PageDefinition>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.OrderNo, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return list;
    }
}
