using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 治理 M-G05-C4（S10）：组织成员 CRUD + 跨组织 workspace 迁移。
/// </summary>
[ApiController]
[Route("api/v1/organizations/{organizationId:long}/members")]
[Authorize]
public sealed class OrganizationMembersController : ControllerBase
{
    private readonly IOrganizationMemberService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public OrganizationMembersController(
        IOrganizationMemberService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrganizationMemberDto>>>> List(long organizationId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _service.ListAsync(tenantId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrganizationMemberDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Add(
        long organizationId,
        [FromBody] OrganizationMemberAddRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.AddAsync(tenantId, organizationId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{userId:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long organizationId,
        long userId,
        [FromBody] OrganizationMemberUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, organizationId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{userId:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Remove(
        long organizationId,
        long userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.RemoveAsync(tenantId, organizationId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}

/// <summary>
/// 治理 M-G05-C5（S10）：跨组织 workspace 迁移。路由独立避免 url 路径冲突。
/// </summary>
[ApiController]
[Route("api/v1/workspaces/{workspaceId:long}")]
[Authorize]
public sealed class WorkspaceOrganizationMoveController : ControllerBase
{
    private readonly IOrganizationMemberService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public WorkspaceOrganizationMoveController(
        IOrganizationMemberService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPatch("move-organization")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Move(
        long workspaceId,
        [FromBody] WorkspaceMoveRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        if (!long.TryParse(request.TargetOrganizationId, out var targetOrgId))
        {
            return BadRequest(ApiResponse<object>.Fail(Atlas.Core.Models.ErrorCodes.ValidationError, "TargetOrganizationIdInvalid", HttpContext.TraceIdentifier));
        }
        await _service.MoveWorkspaceAsync(tenantId, workspaceId, targetOrgId, actor.UserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
