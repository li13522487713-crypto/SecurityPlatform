using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Enums;
using Atlas.Core.Identity;

namespace Atlas.Infrastructure.Services.Platform;

/// <summary>
/// 应用级数据权限过滤器实现（基于 AppRole + AppDepartment，等保2.0 最小化授权）
/// </summary>
public sealed class AppDataScopeFilter : IAppDataScopeFilter
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IAppRoleRepository _appRoleRepository;

    public AppDataScopeFilter(
        ICurrentUserAccessor currentUserAccessor,
        IAppUserRoleRepository appUserRoleRepository,
        IAppRoleRepository appRoleRepository)
    {
        _currentUserAccessor = currentUserAccessor;
        _appUserRoleRepository = appUserRoleRepository;
        _appRoleRepository = appRoleRepository;
    }

    public async Task<DataScopeType> GetEffectiveScopeAsync(long appId, CancellationToken ct = default)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null) return DataScopeType.OnlySelf;

        var userRoles = await _appUserRoleRepository.QueryByUserIdsAsync(
            user.TenantId, appId, [user.UserId], ct);

        if (userRoles.Count == 0) return DataScopeType.OnlySelf;

        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToArray();
        var roles = await _appRoleRepository.QueryByIdsAsync(user.TenantId, appId, roleIds, ct);

        if (roles.Count == 0) return DataScopeType.OnlySelf;

        return roles.Min(r => r.DataScope);
    }

    public async Task<long?> GetOwnerFilterIdAsync(long appId, CancellationToken ct = default)
    {
        var scope = await GetEffectiveScopeAsync(appId, ct);
        if (scope == DataScopeType.OnlySelf)
        {
            var user = _currentUserAccessor.GetCurrentUser();
            return user?.UserId;
        }

        return null;
    }

    public async Task<IReadOnlyList<long>?> GetDeptFilterIdsAsync(long appId, CancellationToken ct = default)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        // null = 不限制；空集合 = 严格限制为无可访问部门（例如未识别到当前用户）
        if (user is null) return Array.Empty<long>();

        var scope = await GetEffectiveScopeAsync(appId, ct);
        if (scope is DataScopeType.All or DataScopeType.CurrentTenant)
        {
            return null;
        }

        if (scope == DataScopeType.CustomDept)
        {
            var userRoles = await _appUserRoleRepository.QueryByUserIdsAsync(
                user.TenantId, appId, [user.UserId], ct);
            var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToArray();
            var roles = await _appRoleRepository.QueryByIdsAsync(user.TenantId, appId, roleIds, ct);

            var deptIds = roles
                .Where(r => r.DataScope == DataScopeType.CustomDept && !string.IsNullOrWhiteSpace(r.DeptIds))
                .SelectMany(r => r.DeptIds!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0L)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            return deptIds;
        }

        // CurrentDept / CurrentDeptAndBelow: requires AppMemberDepartment relationship
        // (not yet implemented). Fall back to no restriction to avoid data loss.
        return null;
    }
}
