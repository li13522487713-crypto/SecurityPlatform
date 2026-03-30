using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class RbacResolver : IRbacResolver
{
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository;

    public RbacResolver(
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionRepository permissionRepository)
    {
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
    }

    public async Task<IReadOnlyList<string>> GetRoleCodesAsync(
        UserAccount account,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(account.Roles))
        {
            foreach (var role in account.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                codes.Add(role);
            }
        }

        var userRoles = await _userRoleRepository.QueryByUserIdAsync(tenantId, account.Id, cancellationToken);
        if (userRoles.Count == 0)
        {
            return codes.ToArray();
        }

        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        var roles = await _roleRepository.QueryByIdsAsync(tenantId, roleIds, cancellationToken);
        foreach (var role in roles)
        {
            codes.Add(role.Code);
        }

        return codes.ToArray();
    }

    public async Task<IReadOnlyList<string>> GetRoleCodesAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var userRoles = await _userRoleRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken);
        if (userRoles.Count == 0)
        {
            return Array.Empty<string>();
        }

        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        var roles = await _roleRepository.QueryByIdsAsync(tenantId, roleIds, cancellationToken);
        return roles.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var userRoles = await _userRoleRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken);
        if (userRoles.Count == 0)
        {
            return Array.Empty<string>();
        }

        var permissionIds = new HashSet<long>();
        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        var rolePermissions = await _rolePermissionRepository.QueryByRoleIdsAsync(tenantId, roleIds, cancellationToken);
        foreach (var permissionId in rolePermissions.Select(x => x.PermissionId))
        {
            permissionIds.Add(permissionId);
        }

        if (permissionIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        var permissions = await _permissionRepository.QueryByIdsAsync(tenantId, permissionIds.ToArray(), cancellationToken);
        return permissions.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<(IReadOnlyList<string> RoleCodes, IReadOnlyList<string> PermissionCodes)> GetRolesAndPermissionsAsync(
        UserAccount account,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var roleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(account.Roles))
        {
            foreach (var role in account.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                roleCodes.Add(role);
            }
        }

        // 单次查询 UserRole（原来 GetRoleCodesAsync + GetPermissionCodesAsync 各查一次，共 2 次，现在合并为 1 次）
        var userRoles = await _userRoleRepository.QueryByUserIdAsync(tenantId, account.Id, cancellationToken);
        if (userRoles.Count == 0)
        {
            return (roleCodes.ToArray(), Array.Empty<string>());
        }

        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();

        // 并行：同时拉取 Role 列表和 RolePermission 关联
        var rolesTask = _roleRepository.QueryByIdsAsync(tenantId, roleIds, cancellationToken);
        var rolePermsTask = _rolePermissionRepository.QueryByRoleIdsAsync(tenantId, roleIds, cancellationToken);
        await Task.WhenAll(rolesTask, rolePermsTask);

        foreach (var role in rolesTask.Result)
        {
            roleCodes.Add(role.Code);
        }

        var permissionIds = rolePermsTask.Result.Select(x => x.PermissionId).Distinct().ToArray();
        if (permissionIds.Length == 0)
        {
            return (roleCodes.ToArray(), Array.Empty<string>());
        }

        var permissions = await _permissionRepository.QueryByIdsAsync(tenantId, permissionIds, cancellationToken);
        var permCodes = permissions.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return (roleCodes.ToArray(), permCodes);
    }
}
