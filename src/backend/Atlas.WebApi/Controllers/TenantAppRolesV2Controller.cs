using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/tenant-app-instances/{appId:long}/roles")]
[Authorize]
public sealed class TenantAppRolesV2Controller : ControllerBase
{
    private readonly ITenantAppRoleQueryService _queryService;
    private readonly ITenantAppRoleCommandService _commandService;
    private readonly IAppRoleAssignmentQueryService _assignmentQueryService;
    private readonly IAppRoleAssignmentCommandService _assignmentCommandService;
    private readonly ILowCodePageRepository _pageRepository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<TenantAppRoleCreateRequest> _createValidator;
    private readonly IValidator<TenantAppRoleUpdateRequest> _updateValidator;
    private readonly IValidator<TenantAppRoleAssignPermissionsRequest> _permissionsValidator;

    public TenantAppRolesV2Controller(
        ITenantAppRoleQueryService queryService,
        ITenantAppRoleCommandService commandService,
        IAppRoleAssignmentQueryService assignmentQueryService,
        IAppRoleAssignmentCommandService assignmentCommandService,
        ILowCodePageRepository pageRepository,
        IDynamicTableRepository dynamicTableRepository,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<TenantAppRoleCreateRequest> createValidator,
        IValidator<TenantAppRoleUpdateRequest> updateValidator,
        IValidator<TenantAppRoleAssignPermissionsRequest> permissionsValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _assignmentQueryService = assignmentQueryService;
        _assignmentCommandService = assignmentCommandService;
        _pageRepository = pageRepository;
        _dynamicTableRepository = dynamicTableRepository;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _permissionsValidator = permissionsValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantAppRoleListItem>>>> Get(
        long appId,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<TenantAppRoleListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("governance-overview")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<TenantAppRoleGovernanceOverview>>> GetGovernanceOverview(
        long appId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetGovernanceOverviewAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<TenantAppRoleGovernanceOverview>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{roleId:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<TenantAppRoleDetail>>> GetById(
        long appId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByIdAsync(tenantId, appId, roleId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<TenantAppRoleDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgRoleNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TenantAppRoleDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        long appId,
        [FromBody] TenantAppRoleCreateRequest request,
        CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "Unauthorized.",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var roleId = await _commandService.CreateAsync(
            tenantId,
            appId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{roleId:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long appId,
        long roleId,
        [FromBody] TenantAppRoleUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "Unauthorized.",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(
            tenantId,
            appId,
            roleId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{roleId:long}/permissions")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePermissions(
        long appId,
        long roleId,
        [FromBody] TenantAppRoleAssignPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        await _permissionsValidator.ValidateAndThrowAsync(request, cancellationToken);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdatePermissionsAsync(tenantId, appId, roleId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{roleId:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long appId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, appId, roleId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用角色的数据范围配置</summary>
    [HttpGet("{roleId:long}/data-scope")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<AppRoleAssignmentDetail>>> GetDataScope(
        long appId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _assignmentQueryService.GetRoleAssignmentAsync(tenantId, appId, roleId, cancellationToken);
        return Ok(ApiResponse<AppRoleAssignmentDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    /// <summary>设置应用角色的数据范围</summary>
    [HttpPut("{roleId:long}/data-scope")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SetDataScope(
        long appId,
        long roleId,
        [FromBody] AppRoleDataScopeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _assignmentCommandService.SetDataScopeAsync(tenantId, appId, roleId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用下所有可分配的页面列表</summary>
    [HttpGet("available-pages")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppPageListItem>>>> GetAvailablePages(
        long appId,
        CancellationToken cancellationToken)
    {
        // 当前阶段以低代码页面作为导航授权载体，不引入独立 app-level menu 实体。
        var tenantId = _tenantProvider.GetTenantId();
        var pages = await _pageRepository.GetByAppIdAsync(tenantId, appId, cancellationToken);
        var result = pages.Select(p => new AppPageListItem(
            p.Id.ToString(),
            p.PageKey,
            p.Name,
            p.Description,
            p.RoutePath,
            p.ParentPageId,
            p.SortOrder,
            p.IsPublished)).ToArray();
        return Ok(ApiResponse<IReadOnlyList<AppPageListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用角色已分配的页面 ID 列表</summary>
    [HttpGet("{roleId:long}/pages")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<long>>>> GetRolePages(
        long appId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var pageIds = await _assignmentQueryService.GetRolePagesAsync(tenantId, appId, roleId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<long>>.Ok(pageIds, HttpContext.TraceIdentifier));
    }

    /// <summary>设置应用角色的页面分配（全量替换）</summary>
    [HttpPut("{roleId:long}/pages")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SetRolePages(
        long appId,
        long roleId,
        [FromBody] AppRolePagesRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _assignmentCommandService.SetRolePagesAsync(tenantId, appId, roleId, request.PageIds, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用角色的字段权限配置</summary>
    [HttpGet("{roleId:long}/field-permissions")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppRoleFieldPermissionGroup>>>> GetRoleFieldPermissions(
        long appId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var groups = await _assignmentQueryService.GetRoleFieldPermissionsAsync(tenantId, appId, roleId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppRoleFieldPermissionGroup>>.Ok(groups, HttpContext.TraceIdentifier));
    }

    /// <summary>设置应用角色的字段权限（全量替换）</summary>
    [HttpPut("{roleId:long}/field-permissions")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SetRoleFieldPermissions(
        long appId,
        long roleId,
        [FromBody] AppRoleFieldPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _assignmentCommandService.SetRoleFieldPermissionsAsync(tenantId, appId, roleId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用下可用于字段权限配置的动态表列表（不依赖 Header 注入）</summary>
    [HttpGet("available-dynamic-tables")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<object>>>> GetAvailableDynamicTables(
        long appId,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var (items, _) = await _dynamicTableRepository.QueryPageAsync(tenantId, 1, 200, keyword, appId, cancellationToken);
        var result = items.Select(t => new { tableKey = t.TableKey, displayName = t.DisplayName }).ToArray();
        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
