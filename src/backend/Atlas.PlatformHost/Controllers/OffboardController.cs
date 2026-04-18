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
/// 治理 M-G06-C3 / C4（S12）：离职移交 + 组织间成员迁移。
/// </summary>
[ApiController]
[Route("api/v1/identity")]
[Authorize]
public sealed class OffboardController : ControllerBase
{
    private readonly IOffboardService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public OffboardController(
        IOffboardService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("offboard")]
    [Authorize(Policy = PermissionPolicies.UsersUpdate)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ResourceOwnershipTransferDto>>>> Offboard(
        [FromBody] OffboardRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var transfers = await _service.ExecuteOffboardAsync(tenantId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ResourceOwnershipTransferDto>>.Ok(transfers, HttpContext.TraceIdentifier));
    }

    [HttpPatch("organizations/{sourceOrganizationId:long}/members/{userId:long}/move")]
    [Authorize(Policy = PermissionPolicies.UsersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> MoveMember(
        long sourceOrganizationId,
        long userId,
        [FromBody] OrganizationMemberMoveRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.MoveMemberAcrossOrganizationsAsync(tenantId, sourceOrganizationId, userId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
