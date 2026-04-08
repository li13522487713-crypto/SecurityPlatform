using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.Identity.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;
using Atlas.Presentation.Shared.Helpers;

namespace Atlas.Presentation.Shared.Controllers.TenantAppV2;

[ApiController]
[Route("api/v2/tenant-app-instances/{appId:long}/organization")]
[Authorize]
public sealed class TenantAppOrganizationController : ControllerBase
{
    private readonly IAppOrganizationQueryService _queryService;
    private readonly IAppOrganizationCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<UserCreateRequest> _userCreateValidator;

    public TenantAppOrganizationController(
        IAppOrganizationQueryService queryService,
        IAppOrganizationCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<UserCreateRequest> userCreateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _userCreateValidator = userCreateValidator;
    }

    [HttpGet("workspace")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<AppOrganizationWorkspaceResponse>>> GetWorkspace(
        long appId,
        [FromQuery] PagedRequest request,
        [FromQuery] long? roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetWorkspaceAsync(tenantId, appId, request, roleId, cancellationToken);
        return Ok(ApiResponse<AppOrganizationWorkspaceResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("members")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> AddMembers(
        long appId,
        [FromBody] AppOrganizationAssignMembersRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _commandService.AddMembersAsync(tenantId, appId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("members/users")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateMemberUser(
        long appId,
        [FromBody] AppOrganizationCreateMemberUserRequest request,
        CancellationToken cancellationToken)
    {
        var createUserRequest = new UserCreateRequest(
            request.Username,
            request.Password,
            request.DisplayName,
            request.Email,
            request.PhoneNumber,
            request.IsActive,
            Array.Empty<long>(),
            Array.Empty<long>(),
            Array.Empty<long>());
        _userCreateValidator.ValidateAndThrow(createUserRequest);

        var tenantId = _tenantProvider.GetTenantId();
        var userId = await _commandService.CreateMemberUserAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(
            new { appId = appId.ToString(), userId = userId.ToString() },
            HttpContext.TraceIdentifier));
    }

    [HttpPut("members/{userId}/roles")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMemberRoles(
        long appId,
        string userId,
        [FromBody] AppOrganizationUpdateMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateMemberRolesAsync(tenantId, appId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), userId }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("members/{userId}")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveMember(
        long appId,
        string userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.RemoveMemberAsync(tenantId, appId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), userId }, HttpContext.TraceIdentifier));
    }

    [HttpPost("members/{userId}/reset-password")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ResetMemberPassword(
        long appId,
        string userId,
        [FromBody] AppOrganizationResetMemberPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, "NewPasswordRequired", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.ResetMemberPasswordAsync(tenantId, appId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), userId }, HttpContext.TraceIdentifier));
    }

    [HttpPut("members/{userId}/profile")]
    [Authorize(Policy = PermissionPolicies.AppMembersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMemberProfile(
        long appId,
        string userId,
        [FromBody] AppOrganizationUpdateMemberProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, "DisplayNameRequired", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateMemberProfileAsync(tenantId, appId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), userId }, HttpContext.TraceIdentifier));
    }

    [HttpPost("roles")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateRole(
        long appId,
        [FromBody] AppOrganizationCreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var roleId = await _commandService.CreateRoleAsync(tenantId, appId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId = roleId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("roles/{roleId}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRole(
        long appId,
        string roleId,
        [FromBody] AppOrganizationUpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _commandService.UpdateRoleAsync(tenantId, appId, roleId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("roles/{roleId}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRole(
        long appId,
        string roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteRoleAsync(tenantId, appId, roleId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), roleId }, HttpContext.TraceIdentifier));
    }

    [HttpPost("departments")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateDepartment(
        long appId,
        [FromBody] AppOrganizationCreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateDepartmentAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("departments/{id}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDepartment(
        long appId,
        string id,
        [FromBody] AppOrganizationUpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateDepartmentAsync(tenantId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("departments/{id}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDepartment(
        long appId,
        string id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteDepartmentAsync(tenantId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id }, HttpContext.TraceIdentifier));
    }

    [HttpPost("positions")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreatePosition(
        long appId,
        [FromBody] AppOrganizationCreatePositionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreatePositionAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("positions/{id}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePosition(
        long appId,
        string id,
        [FromBody] AppOrganizationUpdatePositionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdatePositionAsync(tenantId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("positions/{id}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeletePosition(
        long appId,
        string id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeletePositionAsync(tenantId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id }, HttpContext.TraceIdentifier));
    }

    [HttpPost("projects")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateProject(
        long appId,
        [FromBody] AppOrganizationCreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateProjectAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("projects/{id}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProject(
        long appId,
        string id,
        [FromBody] AppOrganizationUpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateProjectAsync(tenantId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("projects/{id}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProject(
        long appId,
        string id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteProjectAsync(tenantId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { appId = appId.ToString(), id }, HttpContext.TraceIdentifier));
    }
}



