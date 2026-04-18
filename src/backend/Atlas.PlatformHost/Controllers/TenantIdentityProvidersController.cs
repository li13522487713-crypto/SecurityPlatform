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
/// 治理 M-G07-C2（S13）：租户级身份提供方（OIDC / SAML）CRUD。
/// </summary>
[ApiController]
[Route("api/v1/tenant-identity-providers")]
[Authorize]
public sealed class TenantIdentityProvidersController : ControllerBase
{
    private readonly ITenantIdentityProviderService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TenantIdentityProvidersController(
        ITenantIdentityProviderService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantIdentityProviderDto>>>> List(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _service.ListAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TenantIdentityProviderDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.TenantsCreate)]
    public async Task<ActionResult<ApiResponse<TenantIdentityProviderDto>>> Create(
        [FromBody] TenantIdentityProviderCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.CreateAsync(tenantId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<TenantIdentityProviderDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] TenantIdentityProviderUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.UpdateAsync(tenantId, id, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
