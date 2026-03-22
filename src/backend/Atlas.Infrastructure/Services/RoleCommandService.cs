using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Enums;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
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
    private readonly IRoleDeptRepository _roleDeptRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionDecisionService _permissionDecisionService;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public RoleCommandService(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleMenuRepository roleMenuRepository,
        IUserRoleRepository userRoleRepository,
        IPermissionRepository permissionRepository,
        IMenuRepository menuRepository,
        IRoleDeptRepository roleDeptRepository,
        IDepartmentRepository departmentRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        IPermissionDecisionService permissionDecisionService,
        IAuditWriter auditWriter,
        ICurrentUserAccessor currentUserAccessor)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _roleMenuRepository = roleMenuRepository;
        _userRoleRepository = userRoleRepository;
        _permissionRepository = permissionRepository;
        _menuRepository = menuRepository;
        _roleDeptRepository = roleDeptRepository;
        _departmentRepository = departmentRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _permissionDecisionService = permissionDecisionService;
        _auditWriter = auditWriter;
        _currentUserAccessor = currentUserAccessor;
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
            throw new BusinessException("RoleCodeExists", ErrorCodes.ValidationError);
        }

        var role = new Role(tenantId, request.Name, request.Code, id);
        role.Update(request.Name, request.Description);
        await _roleRepository.AddAsync(role, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Role.Created",
            $"roleId={role.Id};code={request.Code};name={request.Name}",
            cancellationToken);
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
            throw new BusinessException("RoleNotFound", ErrorCodes.NotFound);
        }

        role.Update(request.Name, request.Description);
        await _roleRepository.UpdateAsync(role, cancellationToken);
        await _permissionDecisionService.InvalidateRoleAsync(tenantId, roleId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Role.Updated",
            $"roleId={roleId};name={request.Name}",
            cancellationToken);
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

        await _permissionDecisionService.InvalidateRoleAsync(tenantId, roleId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Role.PermissionsUpdated",
            $"roleId={roleId};permissionIds={string.Join(',', permissionIds.Distinct())}",
            cancellationToken);
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

        await _permissionDecisionService.InvalidateRoleAsync(tenantId, roleId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Role.MenusUpdated",
            $"roleId={roleId};menuIds={string.Join(',', menuIds.Distinct())}",
            cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException("RoleNotFound", ErrorCodes.NotFound);
        }

        if (role.IsSystem)
        {
            throw new BusinessException("SystemRoleCannotDelete", ErrorCodes.Forbidden);
        }

        var userIds = await _userRoleRepository.QueryUserIdsByRoleIdAsync(tenantId, roleId, cancellationToken);
        if (userIds.Count > 0)
        {
            throw new BusinessException("RoleHasUsers", ErrorCodes.ValidationError);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _rolePermissionRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _roleMenuRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _userRoleRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            await _roleRepository.DeleteAsync(tenantId, roleId, cancellationToken);
        }, cancellationToken);

        await _permissionDecisionService.InvalidateRoleAsync(tenantId, roleId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Role.Deleted",
            $"roleId={roleId};code={role.Code}",
            cancellationToken);
    }

    public async Task SetDataScopeAsync(TenantId tenantId, long roleId, DataScopeType scope, IReadOnlyList<long>? deptIds, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException("RoleNotFound", ErrorCodes.NotFound);
        }

        var distinctDeptIds = deptIds?.Distinct().ToArray() ?? Array.Empty<long>();
        if (scope == DataScopeType.CustomDept)
        {
            if (distinctDeptIds.Length == 0)
            {
                throw new BusinessException("DataScopeDeptRequired", ErrorCodes.ValidationError);
            }

            var depts = await _departmentRepository.QueryByIdsAsync(tenantId, distinctDeptIds, cancellationToken);
            if (depts.Count != distinctDeptIds.Length)
            {
                throw new BusinessException("DeptNotFound", ErrorCodes.ValidationError);
            }
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            role.SetDataScope(scope);
            await _roleRepository.UpdateAsync(role, cancellationToken);

            await _roleDeptRepository.DeleteByRoleIdAsync(tenantId, roleId, cancellationToken);
            if (scope == DataScopeType.CustomDept && distinctDeptIds.Length > 0)
            {
                await _roleDeptRepository.AddRangeAsync(
                    distinctDeptIds
                        .Select(deptId => new RoleDept(tenantId, roleId, deptId, _idGeneratorAccessor.NextId()))
                        .ToArray(),
                    cancellationToken);
            }
        }, cancellationToken);

        await _permissionDecisionService.InvalidateRoleAsync(tenantId, roleId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Role.DataScopeUpdated",
            $"roleId={roleId};scope={(int)scope};deptIds={string.Join(',', distinctDeptIds)}",
            cancellationToken);
    }

    private async Task EnsureRoleExistsAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException("RoleNotFound", ErrorCodes.NotFound);
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
            throw new BusinessException("PermissionNotFoundPlatform", ErrorCodes.ValidationError);
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
            throw new BusinessException("MenuNotFound", ErrorCodes.ValidationError);
        }
    }

    private async Task WriteAuditAsync(
        TenantId tenantId,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var actor = currentUser?.Username ?? "system";
        var record = new AuditRecord(
            tenantId,
            actor,
            action,
            "Success",
            target,
            null,
            null);
        await _auditWriter.WriteAsync(record, cancellationToken);
    }
}
