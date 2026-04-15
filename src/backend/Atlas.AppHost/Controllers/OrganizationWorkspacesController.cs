using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/organizations/{orgId}/workspaces")]
[Authorize]
public sealed class OrganizationWorkspacesController : ControllerBase
{
    private readonly IWorkspacePortalService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public OrganizationWorkspacesController(
        IWorkspacePortalService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkspaceListItem>>>> List(
        string orgId,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.ListWorkspacesAsync(tenantId, currentUser.UserId, currentUser.IsPlatformAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkspaceListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("by-app-key/{appKey}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<WorkspaceDetailDto>>> GetByAppKey(
        string orgId,
        string appKey,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.GetWorkspaceByAppKeyAsync(tenantId, appKey, currentUser.UserId, currentUser.IsPlatformAdmin, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkspaceDetailDto>.Fail(ErrorCodes.NotFound, "工作空间不存在。", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkspaceDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{workspaceId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<WorkspaceDetailDto>>> Get(
        string orgId,
        long workspaceId,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.GetWorkspaceAsync(tenantId, workspaceId, currentUser.UserId, currentUser.IsPlatformAdmin, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkspaceDetailDto>.Fail(ErrorCodes.NotFound, "工作空间不存在。", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkspaceDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        string orgId,
        [FromBody] WorkspaceCreateRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var id = await _service.CreateWorkspaceAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{workspaceId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        string orgId,
        long workspaceId,
        [FromBody] WorkspaceUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        await _service.UpdateWorkspaceAsync(tenantId, workspaceId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{workspaceId:long}/develop/apps")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceAppCardDto>>>> GetDevelopApps(
        string orgId,
        long workspaceId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.GetDevelopAppsAsync(
            tenantId,
            workspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            new PagedRequest(pageIndex, pageSize, keyword),
            cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkspaceAppCardDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{workspaceId:long}/develop/apps")]
    [Authorize(Policy = PermissionPolicies.AiAppCreate)]
    public async Task<ActionResult<ApiResponse<WorkspaceAppCreateResult>>> CreateDevelopApp(
        string orgId,
        long workspaceId,
        [FromBody] WorkspaceAppCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.CreateDevelopAppAsync(tenantId, workspaceId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<WorkspaceAppCreateResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{workspaceId:long}/resources")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceResourceCardDto>>>> GetResources(
        string orgId,
        long workspaceId,
        [FromQuery] string? resourceType = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.GetResourcesAsync(
            tenantId,
            workspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            resourceType,
            new PagedRequest(pageIndex, pageSize, keyword),
            cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkspaceResourceCardDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{workspaceId:long}/members")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkspaceMemberDto>>>> GetMembers(
        string orgId,
        long workspaceId,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.GetMembersAsync(tenantId, workspaceId, currentUser.UserId, currentUser.IsPlatformAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkspaceMemberDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{workspaceId:long}/members")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> AddMember(
        string orgId,
        long workspaceId,
        [FromBody] WorkspaceMemberCreateRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        await _service.AddMemberAsync(tenantId, workspaceId, currentUser.UserId, currentUser.IsPlatformAdmin, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{workspaceId:long}/members/{userId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMemberRole(
        string orgId,
        long workspaceId,
        long userId,
        [FromBody] WorkspaceMemberRoleUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        await _service.UpdateMemberRoleAsync(tenantId, workspaceId, userId, currentUser.UserId, currentUser.IsPlatformAdmin, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{workspaceId:long}/members/{userId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveMember(
        string orgId,
        long workspaceId,
        long userId,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        await _service.RemoveMemberAsync(tenantId, workspaceId, userId, currentUser.UserId, currentUser.IsPlatformAdmin, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{workspaceId:long}/resources/{resourceType}/{resourceId:long}/permissions")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkspaceRolePermissionDto>>>> GetResourcePermissions(
        string orgId,
        long workspaceId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        var result = await _service.GetResourcePermissionsAsync(
            tenantId,
            workspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            resourceType,
            resourceId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkspaceRolePermissionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{workspaceId:long}/resources/{resourceType}/{resourceId:long}/permissions")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateResourcePermissions(
        string orgId,
        long workspaceId,
        string resourceType,
        long resourceId,
        [FromBody] WorkspaceResourcePermissionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveContext(orgId);
        await _service.UpdateResourcePermissionsAsync(
            tenantId,
            workspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            resourceType,
            resourceId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
    }

    private (TenantId TenantId, CurrentUserInfo CurrentUser) ResolveContext(string orgId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var normalizedOrgId = orgId?.Trim() ?? string.Empty;
        if (!string.Equals(normalizedOrgId, tenantId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCodes.Forbidden, "组织标识与当前租户不匹配。");
        }

        return (tenantId, _currentUserAccessor.GetCurrentUserOrThrow());
    }
}
