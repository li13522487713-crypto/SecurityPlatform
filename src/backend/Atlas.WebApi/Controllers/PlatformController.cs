using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/platform")]
[Authorize]
[PlatformOnly]
public sealed class PlatformController : ControllerBase
{
    private readonly IPlatformQueryService _platformQueryService;
    private readonly ITenantProvider _tenantProvider;

    public PlatformController(IPlatformQueryService platformQueryService, ITenantProvider tenantProvider)
    {
        _platformQueryService = platformQueryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("overview")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PlatformOverviewResponse>>> GetOverview(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _platformQueryService.GetOverviewAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<PlatformOverviewResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("resources")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PlatformResourcesResponse>>> GetResources(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _platformQueryService.GetResourcesAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<PlatformResourcesResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("releases")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppReleaseResponse>>>> GetReleases(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _platformQueryService.GetReleasesAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppReleaseResponse>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
