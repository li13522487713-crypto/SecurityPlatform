using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Enums;
using Atlas.Core.Identity;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Services.Platform;

/// <summary>
/// 应用级数据权限过滤器实现（基于 AppRole + AppDepartment + AppMemberDepartment，等保2.0 最小化授权）
/// </summary>
public sealed class AppDataScopeFilter : IAppDataScopeFilter
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IAppMemberDepartmentRepository _appMemberDeptRepository;
    private readonly IAppDepartmentRepository _appDepartmentRepository;
    private readonly IAppProjectUserRepository _appProjectUserRepository;

    public AppDataScopeFilter(
        ICurrentUserAccessor currentUserAccessor,
        IAppUserRoleRepository appUserRoleRepository,
        IAppRoleRepository appRoleRepository,
        IAppMemberDepartmentRepository appMemberDeptRepository,
        IAppDepartmentRepository appDepartmentRepository,
        IAppProjectUserRepository appProjectUserRepository)
    {
        _currentUserAccessor = currentUserAccessor;
        _appUserRoleRepository = appUserRoleRepository;
        _appRoleRepository = appRoleRepository;
        _appMemberDeptRepository = appMemberDeptRepository;
        _appDepartmentRepository = appDepartmentRepository;
        _appProjectUserRepository = appProjectUserRepository;
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

        if (scope is DataScopeType.CurrentDept or DataScopeType.CurrentDeptAndBelow)
        {
            var memberDepts = await _appMemberDeptRepository.QueryByUserIdAsync(
                user.TenantId, appId, user.UserId, ct);
            var myDeptIds = memberDepts.Select(md => md.DepartmentId).Distinct().ToArray();

            if (myDeptIds.Length == 0) return Array.Empty<long>();

            if (scope == DataScopeType.CurrentDept)
            {
                return myDeptIds;
            }

            var allDepts = await _appDepartmentRepository.QueryByAppIdAsync(user.TenantId, appId, ct);
            return ExpandDeptAndBelow(myDeptIds, allDepts);
        }

        return null;
    }

    public async Task<IReadOnlyList<long>?> GetProjectFilterIdsAsync(long appId, CancellationToken ct = default)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Array.Empty<long>();
        }

        var scope = await GetEffectiveScopeAsync(appId, ct);
        if (scope is DataScopeType.All or DataScopeType.CurrentTenant)
        {
            return null;
        }

        if (scope != DataScopeType.Project)
        {
            return null;
        }

        var rows = await _appProjectUserRepository.QueryByUserIdAsync(user.TenantId, appId, user.UserId, ct);
        return rows.Select(r => r.ProjectId).Distinct().ToArray();
    }

    private static long[] ExpandDeptAndBelow(long[] rootDeptIds, IReadOnlyList<AppDepartment> allDepts)
    {
        var childrenByParent = new Dictionary<long, List<long>>();
        foreach (var dept in allDepts)
        {
            if (dept.ParentId is > 0)
            {
                if (!childrenByParent.TryGetValue(dept.ParentId.Value, out var children))
                {
                    children = [];
                    childrenByParent[dept.ParentId.Value] = children;
                }
                children.Add(dept.Id);
            }
        }

        var result = new HashSet<long>(rootDeptIds);
        var queue = new Queue<long>(rootDeptIds);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var kids)) continue;
            foreach (var kid in kids)
            {
                if (result.Add(kid))
                {
                    queue.Enqueue(kid);
                }
            }
        }

        return [.. result];
    }
}
