using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/tenant-app-instances/{appId:long}/members")]
[Authorize]
public sealed class TenantAppMembersV2Controller : ControllerBase
{
    private readonly ITenantAppMemberQueryService _queryService;
    private readonly ITenantAppMemberCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<TenantAppMemberAssignRequest> _assignValidator;
    private readonly IValidator<TenantAppMemberUpdateRolesRequest> _updateRolesValidator;

    public TenantAppMembersV2Controller(
        ITenantAppMemberQueryService queryService,
        ITenantAppMemberCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<TenantAppMemberAssignRequest> assignValidator,
        IValidator<TenantAppMemberUpdateRolesRequest> updateRolesValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _assignValidator = assignValidator;
        _updateRolesValidator = updateRolesValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppMembersView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantAppMemberListItem>>>> Get(
        long appId,
        [FromQuery] PagedRequest request,
        [FromQuery] long? roleId,
        [FromQuery] long? departmentId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();

        PagedResult<TenantAppMemberListItem> result;
        if (roleId.HasValue && roleId.Value > 0)
        {
            result = await _queryService.QueryByRoleAsync(tenantId, appId, roleId.Value, request, cancellationToken);
        }
        else if (departmentId.HasValue && departmentId.Value > 0)
        {
            result = await _queryService.QueryByDepartmentAsync(tenantId, appId, departmentId.Value, request, cancellationToken);
        }
        else
        {
            result = await _queryService.QueryAsync(tenantId, appId, request, cancellationToken);
        }

        return Ok(ApiResponse<PagedResult<TenantAppMemberListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{userId:long}")]
    [Authorize(Policy = PermissionPolicies.AppMembersView)]
    public async Task<ActionResult<ApiResponse<TenantAppMemberDetail>>> GetByUserId(
        long appId,
        long userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByUserIdAsync(tenantId, appId, userId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<TenantAppMemberDetail>.Fail(
                ErrorCodes.NotFound,
                "应用成员不存在。",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TenantAppMemberDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> AddMembers(
        long appId,
        [FromBody] TenantAppMemberAssignRequest request,
        CancellationToken cancellationToken)
    {
        await _assignValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "Unauthorized.",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.AddMembersAsync(
            tenantId,
            appId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{userId:long}/roles")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMemberRoles(
        long appId,
        long userId,
        [FromBody] TenantAppMemberUpdateRolesRequest request,
        CancellationToken cancellationToken)
    {
        await _updateRolesValidator.ValidateAndThrowAsync(request, cancellationToken);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateMemberRolesAsync(tenantId, appId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), userId = userId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{userId:long}")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveMember(
        long appId,
        long userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.RemoveMemberAsync(tenantId, appId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), userId = userId.ToString() }, HttpContext.TraceIdentifier));
    }
}
