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
/// 治理 M-G08-C1 + C2（S15）：租户网络策略 / 数据驻留策略管理。
/// </summary>
[ApiController]
[Route("api/v1/tenant-policies")]
[Authorize]
public sealed class TenantPoliciesController : ControllerBase
{
    private readonly ITenantPolicyService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TenantPoliciesController(
        ITenantPolicyService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("network")]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<TenantNetworkPolicyDto?>>> GetNetwork(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.GetNetworkAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<TenantNetworkPolicyDto?>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPut("network")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<TenantNetworkPolicyDto>>> UpsertNetwork(
        [FromBody] TenantNetworkPolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.UpsertNetworkAsync(tenantId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<TenantNetworkPolicyDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpGet("data-residency")]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<TenantDataResidencyPolicyDto?>>> GetResidency(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.GetResidencyAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<TenantDataResidencyPolicyDto?>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPut("data-residency")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<TenantDataResidencyPolicyDto>>> UpsertResidency(
        [FromBody] TenantDataResidencyPolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.UpsertResidencyAsync(tenantId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<TenantDataResidencyPolicyDto>.Ok(dto, HttpContext.TraceIdentifier));
    }
}
