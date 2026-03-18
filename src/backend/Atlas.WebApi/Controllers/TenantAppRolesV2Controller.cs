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
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<TenantAppRoleCreateRequest> _createValidator;
    private readonly IValidator<TenantAppRoleUpdateRequest> _updateValidator;
    private readonly IValidator<TenantAppRoleAssignPermissionsRequest> _permissionsValidator;

    public TenantAppRolesV2Controller(
        ITenantAppRoleQueryService queryService,
        ITenantAppRoleCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<TenantAppRoleCreateRequest> createValidator,
        IValidator<TenantAppRoleUpdateRequest> updateValidator,
        IValidator<TenantAppRoleAssignPermissionsRequest> permissionsValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
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
            return NotFound(ApiResponse<TenantAppRoleDetail>.Fail(
                ErrorCodes.NotFound,
                "应用角色不存在。",
                HttpContext.TraceIdentifier));
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
}
