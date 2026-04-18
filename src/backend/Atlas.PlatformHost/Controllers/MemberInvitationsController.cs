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
/// 治理 M-G06-C1（S11）：成员邀请管理 API。
/// </summary>
[ApiController]
[Route("api/v1/member-invitations")]
[Authorize]
public sealed class MemberInvitationsController : ControllerBase
{
    private readonly IMemberInvitationService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MemberInvitationsController(
        IMemberInvitationService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MemberInvitationDto>>>> List(
        [FromQuery] long? organizationId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _service.ListAsync(tenantId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MemberInvitationDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.UsersCreate)]
    public async Task<ActionResult<ApiResponse<MemberInvitationDto>>> Create(
        [FromBody] MemberInvitationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.CreateAsync(tenantId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<MemberInvitationDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/revoke")]
    [Authorize(Policy = PermissionPolicies.UsersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.RevokeAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Accept(
        [FromBody] MemberInvitationAcceptRequest request,
        [FromHeader(Name = "X-Tenant-Id")] string? tenantHeader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out var tenantGuid))
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, "TenantHeaderRequired", HttpContext.TraceIdentifier));
        }
        var tenantId = new TenantId(tenantGuid);
        await _service.AcceptAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
