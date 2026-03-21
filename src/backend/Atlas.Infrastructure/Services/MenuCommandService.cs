using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class MenuCommandService : IMenuCommandService
{
    private readonly IMenuRepository _menuRepository;
    private readonly IRoleMenuRepository _roleMenuRepository;

    public MenuCommandService(IMenuRepository menuRepository, IRoleMenuRepository roleMenuRepository)
    {
        _menuRepository = menuRepository;
        _roleMenuRepository = roleMenuRepository;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        MenuCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        // 非按钮类型才检查路径唯一性
        if (request.MenuType != "F" && !string.IsNullOrWhiteSpace(request.Path))
        {
            var exists = await _menuRepository.ExistsByPathAsync(tenantId, request.Path, null, cancellationToken);
            if (exists)
            {
                throw new BusinessException("菜单路径已存在，请使用不同的路径。", ErrorCodes.ValidationError);
            }
        }

        var menu = new Menu(
            tenantId,
            request.Name,
            request.Path,
            id,
            request.ParentId,
            request.SortOrder,
            request.MenuType,
            request.Component,
            request.Icon,
            request.Perms,
            request.Query,
            request.IsFrame,
            request.IsCache,
            request.Visible,
            request.Status,
            request.PermissionCode,
            request.IsHidden);
        await _menuRepository.AddAsync(menu, cancellationToken);
        return menu.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long menuId,
        MenuUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var menu = await _menuRepository.FindByIdAsync(tenantId, menuId, cancellationToken);
        if (menu is null)
        {
            throw new BusinessException("Menu not found.", ErrorCodes.NotFound);
        }

        // 非按钮类型才检查路径唯一性（排除自身）
        if (request.MenuType != "F" && !string.IsNullOrWhiteSpace(request.Path))
        {
            var exists = await _menuRepository.ExistsByPathAsync(tenantId, request.Path, menuId, cancellationToken);
            if (exists)
            {
                throw new BusinessException("菜单路径已存在，请使用不同的路径。", ErrorCodes.ValidationError);
            }
        }

        menu.Update(
            request.Name,
            request.Path,
            request.ParentId,
            request.SortOrder,
            request.MenuType,
            request.Component,
            request.Icon,
            request.Perms,
            request.Query,
            request.IsFrame,
            request.IsCache,
            request.Visible,
            request.Status,
            request.PermissionCode,
            request.IsHidden);
        await _menuRepository.UpdateAsync(menu, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long menuId,
        CancellationToken cancellationToken)
    {
        var menu = await _menuRepository.FindByIdAsync(tenantId, menuId, cancellationToken);
        if (menu is null)
        {
            throw new BusinessException("菜单不存在。", ErrorCodes.NotFound);
        }

        var hasChildren = await _menuRepository.HasChildrenAsync(tenantId, menuId, cancellationToken);
        if (hasChildren)
        {
            throw new BusinessException("该菜单存在子菜单，请先删除子菜单后再删除本菜单。", ErrorCodes.ValidationError);
        }

        // 级联清理角色-菜单关联
        await _roleMenuRepository.DeleteByMenuIdAsync(tenantId, menuId, cancellationToken);
        await _menuRepository.DeleteAsync(tenantId, menuId, cancellationToken);
    }

    public async Task BatchSortAsync(
        TenantId tenantId,
        MenuBatchSortRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return;
        }

        var updates = request.Items
            .Select(item => (item.MenuId, item.SortOrder))
            .ToList();
        await _menuRepository.BatchUpdateSortOrderAsync(tenantId, updates, cancellationToken);
    }
}
