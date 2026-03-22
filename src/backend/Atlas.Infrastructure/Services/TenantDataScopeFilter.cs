using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Enums;
using Atlas.Core.Identity;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 基于当前用户角色的数据权限过滤器实现（等保2.0 访问控制）
/// </summary>
public sealed class TenantDataScopeFilter : ITenantDataScopeFilter
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IRoleRepository _roleRepository;
    private readonly IRoleDeptRepository _roleDeptRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IProjectUserRepository _projectUserRepository;

    public TenantDataScopeFilter(
        ICurrentUserAccessor currentUserAccessor,
        IRoleRepository roleRepository,
        IRoleDeptRepository roleDeptRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IDepartmentRepository departmentRepository,
        IProjectUserRepository projectUserRepository)
    {
        _currentUserAccessor = currentUserAccessor;
        _roleRepository = roleRepository;
        _roleDeptRepository = roleDeptRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _departmentRepository = departmentRepository;
        _projectUserRepository = projectUserRepository;
    }

    public async Task<DataScopeType> GetEffectiveScopeAsync(CancellationToken ct = default)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null) return DataScopeType.OnlySelf;

        if (user.Roles.Count == 0) return DataScopeType.OnlySelf;

        var roles = await _roleRepository.QueryByCodesAsync(user.TenantId, user.Roles, ct);

        // 取最宽松的数据权限（数值越小越宽松）
        var minScope = roles.Count > 0
            ? roles.Min(r => r.DataScope)
            : DataScopeType.OnlySelf;

        // 全部数据权限仅允许平台管理员生效，其他用户自动降级为当前租户范围。
        if (minScope == DataScopeType.All && !user.IsPlatformAdmin)
        {
            return DataScopeType.CurrentTenant;
        }

        return minScope;
    }

    public async Task<long?> GetOwnerFilterIdAsync(CancellationToken ct = default)
    {
        var scope = await GetEffectiveScopeAsync(ct);

        if (scope == DataScopeType.OnlySelf)
        {
            var user = _currentUserAccessor.GetCurrentUser();
            return user?.UserId;
        }

        return null; // null = 不过滤（有权查看所有）
    }

    public async Task<IReadOnlyList<long>?> GetDeptFilterIdsAsync(CancellationToken ct = default)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            // null = 不限制；空集合 = 严格限制为无可访问部门（例如未识别到当前用户）
            return Array.Empty<long>();
        }

        var scope = await GetEffectiveScopeAsync(ct);
        if (scope is DataScopeType.All or DataScopeType.CurrentTenant)
        {
            return null;
        }

        var userRoles = await _roleRepository.QueryByCodesAsync(user.TenantId, user.Roles, ct);
        if (scope == DataScopeType.CustomDept)
        {
            var roleIds = userRoles.Select(x => x.Id).Distinct().ToArray();
            var roleDepts = await _roleDeptRepository.QueryByRoleIdsAsync(user.TenantId, roleIds, ct);
            return roleDepts.Select(x => x.DeptId).Distinct().ToArray();
        }

        var myDepartments = await _userDepartmentRepository.QueryByUserIdAsync(user.TenantId, user.UserId, ct);
        var myDeptIds = myDepartments.Select(x => x.DepartmentId).Distinct().ToArray();

        if (scope == DataScopeType.CurrentDept)
        {
            return myDeptIds;
        }

        if (scope == DataScopeType.CurrentDeptAndBelow)
        {
            var allDepartments = await _departmentRepository.QueryAllAsync(user.TenantId, ct);
            var childrenByParent = allDepartments
                .Where(x => x.ParentId.HasValue && x.ParentId.Value > 0)
                .GroupBy(x => x.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());

            var result = new HashSet<long>(myDeptIds);
            var queue = new Queue<long>(myDeptIds);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!childrenByParent.TryGetValue(current, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (result.Add(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return result.ToArray();
        }

        return null;
    }

    public async Task<IReadOnlyList<long>?> GetProjectFilterIdsAsync(CancellationToken ct = default)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            // null = 不限制；空集合 = 严格限制为无可访问项目（例如未识别到当前用户）
            return Array.Empty<long>();
        }

        var scope = await GetEffectiveScopeAsync(ct);
        if (scope is DataScopeType.All or DataScopeType.CurrentTenant)
        {
            return null;
        }

        if (scope != DataScopeType.Project)
        {
            return null;
        }

        return await _projectUserRepository.QueryProjectIdsByUserIdAsync(user.TenantId, user.UserId, ct);
    }
}
