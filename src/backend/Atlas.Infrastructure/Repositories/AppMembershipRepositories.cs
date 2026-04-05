using Atlas.Application.Platform.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AppMemberRepository : IAppMemberRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppMemberRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppMemberRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<(IReadOnlyList<AppMember> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<AppMember>()
            .Where(member => member.TenantIdValue == tenantId.Value && member.AppId == appId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            var userIds = await _mainDb.Queryable<UserAccount>()
                .Where(user => user.TenantIdValue == tenantId.Value
                    && (user.Username.Contains(trimmed) || user.DisplayName.Contains(trimmed)))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
            if (userIds.Count == 0)
            {
                return (Array.Empty<AppMember>(), 0);
            }

            query = query.Where(member => SqlFunc.ContainsArray(userIds, member.UserId));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(member => member.JoinedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<AppMember>> QueryByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppMember>> QueryByUserIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<AppMember>();
        }

        var distinctIds = userIds.Distinct().ToArray();
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppMember>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(distinctIds, x.UserId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .AnyAsync();
    }

    public async Task<bool> ExistsAnyAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .AnyAsync();
    }

    public async Task AddRangeAsync(IReadOnlyList<AppMember> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var db = await ResolveDbAsync(new TenantId(entities[0].TenantIdValue), entities[0].AppId, cancellationToken);
        await db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
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

public sealed class AppRoleRepository : IAppRoleRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppRoleRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppRoleRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<(IReadOnlyList<AppRole> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(x => x.Code.Contains(trimmed) || x.Name.Contains(trimmed));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.IsSystem, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<AppRole>> QueryByIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<AppRole>();
        }

        var distinctIds = ids.Distinct().ToArray();
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppRole>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(distinctIds, x.Id))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppRole>> QueryByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.IsSystem, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<AppRole?> FindByIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppRole>()
            .FirstAsync(
                x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == roleId,
                cancellationToken);
    }

    public async Task<AppRole?> FindByCodeAsync(
        TenantId tenantId,
        long appId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppRole>()
            .FirstAsync(
                x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Code == code,
                cancellationToken);
    }

    public async Task AddAsync(AppRole role, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(role.TenantIdValue), role.AppId, cancellationToken);
        await db.Insertable(role).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(AppRole role, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(role.TenantIdValue), role.AppId, cancellationToken);
        await db.Updateable(role)
            .Where(x => x.TenantIdValue == role.TenantIdValue && x.AppId == role.AppId && x.Id == role.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == roleId)
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

public sealed class AppUserRoleRepository : IAppUserRoleRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppUserRoleRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppUserRoleRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppUserRole>> QueryByUserIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<AppUserRole>();
        }

        var distinctIds = userIds.Distinct().ToArray();
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppUserRole>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(distinctIds, x.UserId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppUserRole>> QueryByRoleIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<AppUserRole>();
        }

        var distinctIds = roleIds.Distinct().ToArray();
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppUserRole>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(distinctIds, x.RoleId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task DeleteByUserIdAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.RoleId == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyList<AppUserRole> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var db = await ResolveDbAsync(new TenantId(entities[0].TenantIdValue), entities[0].AppId, cancellationToken);
        await db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
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

public sealed class AppRolePageRepository : IAppRolePageRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppRolePageRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppRolePageRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<long>> QueryPageIdsByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppRolePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.RoleId == roleId)
            .Select(x => x.PageId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task ReplaceAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        IReadOnlyList<long> pageIds,
        IReadOnlyList<AppRolePage> newEntities,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var result = await db.Ado.UseTranAsync(async () =>
        {
            await db.Deleteable<AppRolePage>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.RoleId == roleId)
                .ExecuteCommandAsync(cancellationToken);
            if (newEntities.Count > 0)
            {
                await db.Insertable(newEntities.ToList()).ExecuteCommandAsync(cancellationToken);
            }
        });
        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("替换应用角色页面分配失败。");
        }
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

public sealed class AppRolePermissionRepository : IAppRolePermissionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppRolePermissionRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppRolePermissionRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppRolePermission>> QueryByRoleIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<AppRolePermission>();
        }

        var distinctIds = roleIds.Distinct().ToArray();
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppRolePermission>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(distinctIds, x.RoleId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task DeleteByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppRolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.RoleId == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyList<AppRolePermission> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var db = await ResolveDbAsync(new TenantId(entities[0].TenantIdValue), entities[0].AppId, cancellationToken);
        await db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
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

public sealed class AppMemberDepartmentRepository : IAppMemberDepartmentRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppMemberDepartmentRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppMemberDepartmentRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppMemberDepartment>> QueryByAppIdAsync(
        TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppMemberDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppMemberDepartment>> QueryByUserIdAsync(
        TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppMemberDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppMemberDepartment>> QueryByDepartmentIdAsync(
        TenantId tenantId, long appId, long departmentId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppMemberDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<long>> QueryUserIdsByDepartmentIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken = default)
    {
        if (departmentIds.Count == 0)
        {
            return Array.Empty<long>();
        }

        var ids = departmentIds.Distinct().ToArray();
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppMemberDepartment>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(ids, x.DepartmentId))
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        return list.Distinct().ToArray();
    }

    public async Task AddRangeAsync(IReadOnlyList<AppMemberDepartment> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0) return;
        var db = await ResolveDbAsync(new TenantId(entities[0].TenantIdValue), entities[0].AppId, cancellationToken);
        await db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByUserIdAsync(TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppMemberDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByDepartmentIdAsync(TenantId tenantId, long appId, long departmentId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppMemberDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.DepartmentId == departmentId)
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

public sealed class AppProjectUserRepository : IAppProjectUserRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppProjectUserRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public AppProjectUserRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppProjectUser>> QueryByUserIdAsync(
        TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppProjectUser>> QueryByProjectIdAsync(
        TenantId tenantId, long appId, long projectId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task AddRangeAsync(IReadOnlyList<AppProjectUser> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var db = await ResolveDbAsync(new TenantId(entities[0].TenantIdValue), entities[0].AppId, cancellationToken);
        await db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByUserIdAsync(
        TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
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

public sealed class AppMemberPositionRepository : IAppMemberPositionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppMemberPositionRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppMemberPositionRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<IReadOnlyList<AppMemberPosition>> QueryByUserIdAsync(
        TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var list = await db.Queryable<AppMemberPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task AddRangeAsync(IReadOnlyList<AppMemberPosition> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0) return;
        var db = await ResolveDbAsync(new TenantId(entities[0].TenantIdValue), entities[0].AppId, cancellationToken);
        await db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByUserIdAsync(TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppMemberPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
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

public sealed class AppPermissionRepository : IAppPermissionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppPermissionRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public AppPermissionRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<AppPermission?> FindByIdAsync(
        TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<AppPermission?> FindByCodeAsync(
        TenantId tenantId, long appId, string code, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        return await db.Queryable<AppPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AppPermission> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, string? type,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<AppPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => x.Type == type);
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Code)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(AppPermission entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(AppPermission entity, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Updateable(entity)
            .Where(x => x.TenantIdValue == entity.TenantIdValue && x.AppId == entity.AppId && x.Id == entity.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbAsync(tenantId, appId, cancellationToken);
        await db.Deleteable<AppPermission>()
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
