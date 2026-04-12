using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class RbacResolver : IRbacResolver
{
    private static readonly string WildcardPermission = "*:*:*";

    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IAppRolePermissionRepository _appRolePermissionRepository;

    public RbacResolver(
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionRepository permissionRepository,
        IAppContextAccessor appContextAccessor,
        IAppUserRoleRepository appUserRoleRepository,
        IAppRoleRepository appRoleRepository,
        IAppRolePermissionRepository appRolePermissionRepository)
    {
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
        _appContextAccessor = appContextAccessor;
        _appUserRoleRepository = appUserRoleRepository;
        _appRoleRepository = appRoleRepository;
        _appRolePermissionRepository = appRolePermissionRepository;
    }

    public async Task<IReadOnlyList<string>> GetRoleCodesAsync(
        UserAccount account,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var appId = ResolveAppId();
        if (appId.HasValue)
        {
            var (roleCodes, _) = await GetAppRolesAndPermissionsAsync(tenantId, account.Id, appId.Value, cancellationToken);
            return roleCodes;
        }

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
        var appId = ResolveAppId();
        if (appId.HasValue)
        {
            var (roleCodes, _) = await GetAppRolesAndPermissionsAsync(tenantId, userId, appId.Value, cancellationToken);
            return roleCodes;
        }

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
        var appId = ResolveAppId();
        if (appId.HasValue)
        {
            var (_, permissionCodes) = await GetAppRolesAndPermissionsAsync(tenantId, userId, appId.Value, cancellationToken);
            return permissionCodes;
        }

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
        var appId = ResolveAppId();
        if (appId.HasValue)
        {
            return await GetAppRolesAndPermissionsAsync(tenantId, account.Id, appId.Value, cancellationToken);
        }

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

    private long? ResolveAppId()
    {
        var appId = _appContextAccessor.ResolveAppId();
        return appId is > 0 ? appId : null;
    }

    private async Task<(IReadOnlyList<string> RoleCodes, IReadOnlyList<string> PermissionCodes)> GetAppRolesAndPermissionsAsync(
        TenantId tenantId,
        long userId,
        long appId,
        CancellationToken cancellationToken)
    {
        var appUserRoles = await _appUserRoleRepository.QueryByUserIdsAsync(tenantId, appId, [userId], cancellationToken);
        if (appUserRoles.Count == 0)
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var roleIds = appUserRoles.Select(item => item.RoleId).Distinct().ToArray();
        var rolesTask = _appRoleRepository.QueryByIdsAsync(tenantId, appId, roleIds, cancellationToken);
        var rolePermissionsTask = _appRolePermissionRepository.QueryByRoleIdsAsync(tenantId, appId, roleIds, cancellationToken);
        await Task.WhenAll(rolesTask, rolePermissionsTask);

        var roleCodes = rolesTask.Result
            .Select(role => role.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissionCodes = rolePermissionsTask.Result
            .Select(item => item.PermissionCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roleCodes.Count > 0)
        {
            permissionCodes.Add(PermissionCodes.AppUser);
        }

        if (roleCodes.Contains("AppAdmin"))
        {
            permissionCodes.Add(PermissionCodes.AppAdmin);
            permissionCodes.Add(WildcardPermission);
        }

        return (roleCodes.ToArray(), permissionCodes.ToArray());
    }
}
