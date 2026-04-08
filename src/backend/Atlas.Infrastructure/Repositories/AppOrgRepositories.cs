using Atlas.Application.Platform.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AppDepartmentRepository : IAppDepartmentRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppDepartmentRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public AppDepartmentRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppDepartment>> QueryByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AppDepartment> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        var total = await query.CountAsync(cancellationToken);
        var list = await query.OrderBy(x => x.SortOrder).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (list, total);
    }

    public async Task<AppDepartment?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppDepartment>> QueryByIdsAsync(TenantId tenantId, long appId, IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AppDepartment entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId: new TenantId(entity.TenantIdValue), appId: entity.AppId, cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(AppDepartment entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId: new TenantId(entity.TenantIdValue), appId: entity.AppId, cancellationToken);
        await db.Updateable(entity)
            .Where(x => x.TenantIdValue == entity.TenantIdValue && x.AppId == entity.AppId && x.Id == entity.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        if (appId > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
        }

        return _mainDb;
    }
}

public sealed class AppPositionRepository : IAppPositionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    public AppPositionRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppPositionRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppPosition>> QueryByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AppPosition> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        var total = await query.CountAsync(cancellationToken);
        var list = await query.OrderBy(x => x.SortOrder).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (list, total);
    }

    public async Task<AppPosition?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task AddAsync(AppPosition entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(AppPosition entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Updateable(entity)
            .Where(x => x.TenantIdValue == entity.TenantIdValue && x.AppId == entity.AppId && x.Id == entity.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        if (appId > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
        }

        return _mainDb;
    }
}

public sealed class AppProjectRepository : IAppProjectRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    public AppProjectRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppProjectRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppProject>> QueryByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AppProject> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        var total = await query.CountAsync(cancellationToken);
        var list = await query.OrderBy(x => x.Id, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (list, total);
    }

    public async Task<AppProject?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task AddAsync(AppProject entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(AppProject entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Updateable(entity)
            .Where(x => x.TenantIdValue == entity.TenantIdValue && x.AppId == entity.AppId && x.Id == entity.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        if (appId > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
        }

        return _mainDb;
    }
}
