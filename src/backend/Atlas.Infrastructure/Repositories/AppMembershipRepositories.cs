using Atlas.Application.Platform.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AppMemberRepository : IAppMemberRepository
{
    private readonly ISqlSugarClient _db;

    public AppMemberRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<AppMember> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<AppMember, UserAccount>(
                (member, user) => new JoinQueryInfos(
                    JoinType.Inner,
                    member.TenantIdValue == user.TenantIdValue && member.UserId == user.Id))
            .Where((member, user) => member.TenantIdValue == tenantId.Value && member.AppId == appId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where((member, user) =>
                user.Username.Contains(trimmed) || user.DisplayName.Contains(trimmed));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy((member, user) => member.JoinedAt, OrderByType.Desc)
            .Select((member, user) => member)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<AppMember>> QueryByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var list = await _db.Queryable<AppMember>()
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
        var list = await _db.Queryable<AppMember>()
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
        return await _db.Queryable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .AnyAsync();
    }

    public async Task<bool> ExistsAnyAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .AnyAsync();
    }

    public Task AddRangeAsync(IReadOnlyList<AppMember> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class AppRoleRepository : IAppRoleRepository
{
    private readonly ISqlSugarClient _db;

    public AppRoleRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<AppRole> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<AppRole>()
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
        var list = await _db.Queryable<AppRole>()
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
        var list = await _db.Queryable<AppRole>()
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
        return await _db.Queryable<AppRole>()
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
        return await _db.Queryable<AppRole>()
            .FirstAsync(
                x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Code == code,
                cancellationToken);
    }

    public Task AddAsync(AppRole role, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(role).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(AppRole role, CancellationToken cancellationToken = default)
    {
        return _db.Updateable(role)
            .Where(x => x.TenantIdValue == role.TenantIdValue && x.AppId == role.AppId && x.Id == role.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class AppUserRoleRepository : IAppUserRoleRepository
{
    private readonly ISqlSugarClient _db;

    public AppUserRoleRepository(ISqlSugarClient db)
    {
        _db = db;
    }

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
        var list = await _db.Queryable<AppUserRole>()
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
        var list = await _db.Queryable<AppUserRole>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(distinctIds, x.RoleId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByUserIdAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.RoleId == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<AppUserRole> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class AppRolePermissionRepository : IAppRolePermissionRepository
{
    private readonly ISqlSugarClient _db;

    public AppRolePermissionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

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
        var list = await _db.Queryable<AppRolePermission>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.AppId == appId
                && SqlFunc.ContainsArray(distinctIds, x.RoleId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<AppRolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.RoleId == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<AppRolePermission> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
