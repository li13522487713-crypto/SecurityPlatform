using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/capabilities")]
public sealed class CapabilitiesController : ControllerBase
{
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly ITenantProvider _tenantProvider;

    public CapabilitiesController(
        ICapabilityRegistry capabilityRegistry,
        ITenantProvider tenantProvider)
    {
        _capabilityRegistry = capabilityRegistry;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CapabilityManifestItem>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _capabilityRegistry.GetAllAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CapabilityManifestItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{capabilityKey}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<CapabilityManifestItem>>> GetByKey(
        string capabilityKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _capabilityRegistry.GetByKeyAsync(tenantId, capabilityKey, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<CapabilityManifestItem>.Fail(
                ErrorCodes.NotFound,
                "Capability not found.",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<CapabilityManifestItem>.Ok(result, HttpContext.TraceIdentifier));
    }
}
