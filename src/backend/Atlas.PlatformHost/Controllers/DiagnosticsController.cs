using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/admin/diagnostics")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class DiagnosticsController : ControllerBase
{
    private readonly IMenuRepository _menuRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRoleMenuRepository _roleMenuRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITenantProvider _tenantProvider;

    public DiagnosticsController(
        IMenuRepository menuRepository,
        IPermissionRepository permissionRepository,
        IRoleMenuRepository roleMenuRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantProvider tenantProvider)
    {
        _menuRepository = menuRepository;
        _permissionRepository = permissionRepository;
        _roleMenuRepository = roleMenuRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// 诊断权限-菜单绑定一致性：检测菜单可见但 API 无对应权限，或权限存在但无菜单入口的不一致情况。
    /// </summary>
    [HttpGet("permission-menu")]
    public async Task<ActionResult<ApiResponse<PermissionMenuDiagnosticReport>>> DiagnosePermissionMenuBinding(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var allMenus = await _menuRepository.QueryAllAsync(tenantId, cancellationToken);
        var allPermissions = await _permissionRepository.QueryAllAsync(tenantId, cancellationToken);

        var menuPermCodes = allMenus
            .Where(m => !string.IsNullOrWhiteSpace(m.PermissionCode))
            .Select(m => m.PermissionCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var permCodes = allPermissions
            .Select(p => p.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 菜单引用的权限码不存在于权限表
        var menuPermsWithoutDefinition = allMenus
            .Where(m => !string.IsNullOrWhiteSpace(m.PermissionCode)
                        && !permCodes.Contains(m.PermissionCode!))
            .Select(m => new MenuPermIssue(
                m.Id.ToString(),
                m.Name,
                m.Path,
                m.PermissionCode!,
                "菜单引用的权限码未在权限表中定义"))
            .ToList();

        // 权限存在于权限表但无任何菜单使用（可能是孤立权限）
        var orphanPermissions = allPermissions
            .Where(p => p.Type == "Api" && !menuPermCodes.Contains(p.Code))
            .Select(p => new OrphanPermission(p.Id.ToString(), p.Code, p.Name))
            .ToList();

        // 非按钮类型菜单（M/C）无权限码（可见但无法授权）
        var menusWithoutPermCode = allMenus
            .Where(m => m.MenuType is "C" or "M" && string.IsNullOrWhiteSpace(m.PermissionCode) && !m.IsHidden)
            .Select(m => new MenuPermIssue(
                m.Id.ToString(),
                m.Name,
                m.Path,
                string.Empty,
                "菜单缺少权限码配置"))
            .ToList();

        var report = new PermissionMenuDiagnosticReport(
            menuPermsWithoutDefinition,
            orphanPermissions,
            menusWithoutPermCode,
            menuPermsWithoutDefinition.Count + menusWithoutPermCode.Count > 0);

        return Ok(ApiResponse<PermissionMenuDiagnosticReport>.Ok(report, HttpContext.TraceIdentifier));
    }
}

public sealed record PermissionMenuDiagnosticReport(
    IReadOnlyList<MenuPermIssue> MenuPermsWithoutDefinition,
    IReadOnlyList<OrphanPermission> OrphanPermissions,
    IReadOnlyList<MenuPermIssue> MenusWithoutPermCode,
    bool HasIssues);

public sealed record MenuPermIssue(string MenuId, string MenuName, string MenuPath, string PermissionCode, string Issue);
public sealed record OrphanPermission(string PermissionId, string Code, string Name);
