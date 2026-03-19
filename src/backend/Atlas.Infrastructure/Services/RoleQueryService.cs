using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class RoleQueryService : IRoleQueryService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRoleDeptRepository _roleDeptRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleMenuRepository _roleMenuRepository;
    private readonly IMapper _mapper;

    public RoleQueryService(
        IRoleRepository roleRepository,
        IRoleDeptRepository roleDeptRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleMenuRepository roleMenuRepository,
        IMapper mapper)
    {
        _roleRepository = roleRepository;
        _roleDeptRepository = roleDeptRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _roleMenuRepository = roleMenuRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<RoleListItem>> QueryRolesAsync(
        RoleQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _roleRepository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            request.IsSystem,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<RoleListItem>(x)).ToArray();
        return new PagedResult<RoleListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<RoleDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var deptIds = await _roleDeptRepository.QueryByRoleIdsAsync(tenantId, new[] { id }, cancellationToken);
        var permissionIds = await _rolePermissionRepository.QueryByRoleIdAsync(tenantId, id, cancellationToken);
        var menuIds = await _roleMenuRepository.QueryByRoleIdAsync(tenantId, id, cancellationToken);

        return new RoleDetail(
            role.Id.ToString(),
            role.Name,
            role.Code,
            role.Description,
            role.IsSystem,
            (int)role.DataScope,
            deptIds.Select(x => x.DeptId).Distinct().ToArray(),
            permissionIds.Select(x => x.PermissionId).ToArray(),
            menuIds.Select(x => x.MenuId).ToArray());
    }
}
