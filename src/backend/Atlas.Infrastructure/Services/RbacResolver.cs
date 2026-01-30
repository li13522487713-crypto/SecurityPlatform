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
        foreach (var roleId in userRoles.Select(x => x.RoleId).Distinct())
        {
            var rolePermissions = await _rolePermissionRepository.QueryByRoleIdAsync(tenantId, roleId, cancellationToken);
            foreach (var permissionId in rolePermissions.Select(x => x.PermissionId))
            {
                permissionIds.Add(permissionId);
            }
        }

        if (permissionIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        var permissions = await _permissionRepository.QueryByIdsAsync(tenantId, permissionIds.ToArray(), cancellationToken);
        return permissions.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
