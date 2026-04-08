using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.Repositories;

public interface IAppManifestRepository
{
    Task<PagedResult<AppManifest>> QueryAsync(TenantId tenantId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AppManifest?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByKeyAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default);
    Task InsertAsync(AppManifest entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppManifest entity, CancellationToken cancellationToken = default);
}

public interface IAppReleaseRepository
{
    Task<PagedResult<AppRelease>> QueryAsync(TenantId tenantId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<AppRelease?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<int> GetLatestVersionAsync(TenantId tenantId, long manifestId, CancellationToken cancellationToken = default);
    Task InsertAsync(AppRelease entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppRelease entity, CancellationToken cancellationToken = default);
}

public interface IRuntimeRouteRepository
{
    Task<RuntimeRoute?> GetByAppAndPageKeyAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default);
    Task UpsertAsync(RuntimeRoute route, CancellationToken cancellationToken = default);
}

public interface IAppMemberRepository
{
    Task<(IReadOnlyList<AppMember> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppMember>> QueryByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppMember>> QueryByUserIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAnyAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<AppMember> entities, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default);
}

public interface IAppMemberDepartmentRepository
{
    Task<IReadOnlyList<AppMemberDepartment>> QueryByAppIdAsync(
        TenantId tenantId, long appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppMemberDepartment>> QueryByUserIdAsync(
        TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppMemberDepartment>> QueryByDepartmentIdAsync(
        TenantId tenantId, long appId, long departmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 单次查询：返回在任一指定应用部门下的成员用户 ID（去重）。
    /// </summary>
    Task<IReadOnlyList<long>> QueryUserIdsByDepartmentIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<AppMemberDepartment> entities, CancellationToken cancellationToken = default);

    Task DeleteByUserIdAsync(TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default);

    Task DeleteByDepartmentIdAsync(TenantId tenantId, long appId, long departmentId, CancellationToken cancellationToken = default);
}

public interface IAppMemberPositionRepository
{
    Task<IReadOnlyList<AppMemberPosition>> QueryByUserIdAsync(
        TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<AppMemberPosition> entities, CancellationToken cancellationToken = default);

    Task DeleteByUserIdAsync(TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default);
}

public interface IAppRoleRepository
{
    Task<(IReadOnlyList<AppRole> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        string? keyword,
        bool? isSystem = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppRole>> QueryByIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppRole>> QueryByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default);

    Task<AppRole?> FindByIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default);

    Task<AppRole?> FindByCodeAsync(
        TenantId tenantId,
        long appId,
        string code,
        CancellationToken cancellationToken = default);

    Task AddAsync(AppRole role, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppRole role, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default);
}

public interface IAppUserRoleRepository
{
    Task<IReadOnlyList<AppUserRole>> QueryByUserIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppUserRole>> QueryByRoleIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken = default);

    Task DeleteByUserIdAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default);

    Task DeleteByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<AppUserRole> entities, CancellationToken cancellationToken = default);
}

public interface IAppRolePermissionRepository
{
    Task<IReadOnlyList<AppRolePermission>> QueryByRoleIdsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken = default);

    Task DeleteByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<AppRolePermission> entities, CancellationToken cancellationToken = default);
}

public interface IAppRolePageRepository
{
    Task<IReadOnlyList<long>> QueryPageIdsByRoleIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        IReadOnlyList<long> pageIds,
        IReadOnlyList<AppRolePage> newEntities,
        CancellationToken cancellationToken = default);
}

public interface IAppDepartmentRepository
{
    Task<IReadOnlyList<AppDepartment>> QueryByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AppDepartment> Items, int TotalCount)> QueryPageAsync(TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AppDepartment?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppDepartment>> QueryByIdsAsync(TenantId tenantId, long appId, IReadOnlyList<long> ids, CancellationToken cancellationToken = default);
    Task AddAsync(AppDepartment entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppDepartment entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppPositionRepository
{
    Task<IReadOnlyList<AppPosition>> QueryByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AppPosition> Items, int TotalCount)> QueryPageAsync(TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AppPosition?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    Task AddAsync(AppPosition entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppPosition entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppPermissionRepository
{
    Task<AppPermission?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    Task<AppPermission?> FindByCodeAsync(TenantId tenantId, long appId, string code, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AppPermission> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, string? type, CancellationToken cancellationToken = default);
    Task AddAsync(AppPermission entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppPermission entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppProjectRepository
{
    Task<IReadOnlyList<AppProject>> QueryByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AppProject> Items, int TotalCount)> QueryPageAsync(TenantId tenantId, long appId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AppProject?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    Task AddAsync(AppProject entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppProject entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppProjectUserRepository
{
    Task<IReadOnlyList<AppProjectUser>> QueryByUserIdAsync(
        TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppProjectUser>> QueryByProjectIdAsync(
        TenantId tenantId, long appId, long projectId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<AppProjectUser> entities, CancellationToken cancellationToken = default);

    Task DeleteByUserIdAsync(TenantId tenantId, long appId, long userId, CancellationToken cancellationToken = default);
}
