using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Enums;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class RoleCommandService : IRoleCommandService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleMenuRepository _roleMenuRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMenuRepository _menuRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public RoleCommandService(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleMenuRepository roleMenuRepository,
        IUserRoleRepository userRoleRepository,
        IPermissionRepository permissionRepository,
        IMenuRepository menuRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _roleMenuRepository = roleMenuRepository;
        _userRoleRepository = userRoleRepository;
        _permissionRepository = permissionRepository;
        _menuRepository = menuRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
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

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _rolePermissionRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _rolePermissionRepository.AddRangeAsync(
                permissionIds.Distinct()
                    .Select(permissionId => new RolePermission(tenantId, roleId, permissionId, _idGeneratorAccessor.NextId()))
                    .ToArray(),
                cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateMenusAsync(
        TenantId tenantId,
        long roleId,
        IReadOnlyList<long> menuIds,
        CancellationToken cancellationToken)
    {
        await EnsureRoleExistsAsync(tenantId, roleId, cancellationToken);
        await EnsureMenusExistAsync(tenantId, menuIds, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _roleMenuRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _roleMenuRepository.AddRangeAsync(
                menuIds.Distinct()
                    .Select(menuId => new RoleMenu(tenantId, roleId, menuId, _idGeneratorAccessor.NextId()))
                    .ToArray(),
                cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException("Role not found.", ErrorCodes.NotFound);
        }

        if (role.IsSystem)
        {
            throw new BusinessException("System role cannot be deleted.", ErrorCodes.Forbidden);
        }

        var userIds = await _userRoleRepository.QueryUserIdsByRoleIdAsync(tenantId, roleId, cancellationToken);
        if (userIds.Count > 0)
        {
            throw new BusinessException("Role has assigned users, please unbind users before deleting.", ErrorCodes.ValidationError);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _rolePermissionRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _roleMenuRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _userRoleRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _roleRepository.DeleteAsync(tenantId, roleId, cancellationToken);
        }, cancellationToken);
    }

    public async Task SetDataScopeAsync(TenantId tenantId, long roleId, DataScopeType scope, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException("Role not found.", ErrorCodes.NotFound);
        }

        role.SetDataScope(scope);
        await _roleRepository.UpdateAsync(role, cancellationToken);
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
