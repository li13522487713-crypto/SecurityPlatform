using Atlas.Application.DynamicViews.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicViewRepository : IDynamicViewRepository
{
    private readonly ISqlSugarClient _db;

    public DynamicViewRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<DynamicViewDefinition> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long? appId,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(tenantId, appId);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(x => x.ViewKey.Contains(request.Keyword!) || x.Name.Contains(request.Keyword!));
        }

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<DynamicViewDefinition?> FindByKeyAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken)
    {
        return await BuildQuery(tenantId, appId)
            .Where(x => x.ViewKey == viewKey)
            .FirstAsync(cancellationToken);
    }

    public Task AddAsync(DynamicViewDefinition entity, CancellationToken cancellationToken)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(DynamicViewDefinition entity, CancellationToken cancellationToken)
    {
        return _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long? appId, long id, CancellationToken cancellationToken)
    {
        var query = _db.Deleteable<DynamicViewDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id);
        query = appId.HasValue
            ? query.Where(x => x.AppId == appId.Value)
            : query.Where(x => x.AppId == null);

        return query.ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DynamicViewDefinition>> FindReferencingViewAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken)
    {
        var marker = $"\"viewKey\":\"{viewKey}\"";
        var items = await BuildQuery(tenantId, appId)
            .Where(x => x.DefinitionJson.Contains(marker) || x.DraftDefinitionJson.Contains(marker))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<IReadOnlyList<DynamicViewDefinition>> FindByTableReferenceAsync(
        TenantId tenantId,
        long? appId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var marker = $"\"tableKey\":\"{tableKey}\"";
        var items = await BuildQuery(tenantId, appId)
            .Where(x => x.DefinitionJson.Contains(marker) || x.DraftDefinitionJson.Contains(marker))
            .ToListAsync(cancellationToken);
        return items;
    }

    private ISugarQueryable<DynamicViewDefinition> BuildQuery(TenantId tenantId, long? appId)
    {
        var query = _db.Queryable<DynamicViewDefinition>().Where(x => x.TenantIdValue == tenantId.Value);
        return appId.HasValue
            ? query.Where(x => x.AppId == appId.Value)
            : query.Where(x => x.AppId == null);
    }
}
