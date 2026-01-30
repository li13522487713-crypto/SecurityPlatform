using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class RoleCommandService : IRoleCommandService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleMenuRepository _roleMenuRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMenuRepository _menuRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly ISqlSugarClient _db;

    public RoleCommandService(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleMenuRepository roleMenuRepository,
        IPermissionRepository permissionRepository,
        IMenuRepository menuRepository,
        IIdGenerator idGenerator,
        ISqlSugarClient db)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _roleMenuRepository = roleMenuRepository;
        _permissionRepository = permissionRepository;
        _menuRepository = menuRepository;
        _idGenerator = idGenerator;
        _db = db;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        RoleCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.FindByCodeAsync(tenantId, request.Code, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException("Role code already exists.", ErrorCodes.ValidationError);
        }

        var role = new Role(tenantId, request.Name, request.Code, id);
        role.Update(request.Name, request.Description);
        await _roleRepository.AddAsync(role, cancellationToken);
        return role.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long roleId,
        RoleUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException("Role not found.", ErrorCodes.NotFound);
        }

        role.Update(request.Name, request.Description);
        await _roleRepository.UpdateAsync(role, cancellationToken);
    }

    public async Task UpdatePermissionsAsync(
        TenantId tenantId,
        long roleId,
        IReadOnlyList<long> permissionIds,
        CancellationToken cancellationToken)
    {
        await EnsureRoleExistsAsync(tenantId, roleId, cancellationToken);
        await EnsurePermissionsExistAsync(tenantId, permissionIds, cancellationToken);

        await _db.Ado.UseTranAsync(async () =>
        {
            await _rolePermissionRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _rolePermissionRepository.AddRangeAsync(
                permissionIds.Distinct()
                    .Select(permissionId => new RolePermission(tenantId, roleId, permissionId, _idGenerator.NextId()))
                    .ToArray(),
                cancellationToken);
        });
    }

    public async Task UpdateMenusAsync(
        TenantId tenantId,
        long roleId,
        IReadOnlyList<long> menuIds,
        CancellationToken cancellationToken)
    {
        await EnsureRoleExistsAsync(tenantId, roleId, cancellationToken);
        await EnsureMenusExistAsync(tenantId, menuIds, cancellationToken);

        await _db.Ado.UseTranAsync(async () =>
        {
            await _roleMenuRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _roleMenuRepository.AddRangeAsync(
                menuIds.Distinct()
                    .Select(menuId => new RoleMenu(tenantId, roleId, menuId, _idGenerator.NextId()))
                    .ToArray(),
                cancellationToken);
        });
    }

    private async Task EnsureRoleExistsAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException("Role not found.", ErrorCodes.NotFound);
        }
    }

    private async Task EnsurePermissionsExistAsync(
        TenantId tenantId,
        IReadOnlyList<long> permissionIds,
        CancellationToken cancellationToken)
    {
        if (permissionIds.Count == 0)
        {
            return;
        }

        var distinctIds = permissionIds.Distinct().ToArray();
        var permissions = await _permissionRepository.QueryByIdsAsync(tenantId, distinctIds, cancellationToken);
        if (permissions.Count != distinctIds.Length)
        {
            throw new BusinessException("Permission not found.", ErrorCodes.ValidationError);
        }
    }

    private async Task EnsureMenusExistAsync(
        TenantId tenantId,
        IReadOnlyList<long> menuIds,
        CancellationToken cancellationToken)
    {
        if (menuIds.Count == 0)
        {
            return;
        }

        var distinctIds = menuIds.Distinct().ToArray();
        var menus = await _menuRepository.QueryByIdsAsync(tenantId, distinctIds, cancellationToken);
        if (menus.Count != distinctIds.Length)
        {
            throw new BusinessException("Menu not found.", ErrorCodes.ValidationError);
        }
    }
}
