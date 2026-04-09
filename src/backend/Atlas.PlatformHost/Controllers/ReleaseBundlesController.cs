using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/release-bundles")]
[Authorize]
public sealed class ReleaseBundlesController : ControllerBase
{
    private readonly IReleaseBundleQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public ReleaseBundlesController(
        IReleaseBundleQueryService queryService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("releases/{releaseId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ReleaseBundleResponse>>> GetByReleaseId(
        long releaseId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByReleaseIdAsync(tenantId, releaseId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ReleaseBundleResponse>.Fail(ErrorCodes.NotFound, "ReleaseBundle not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ReleaseBundleResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("manifests/{manifestId:long}/active")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ReleaseBundleResponse>>> GetActiveByManifestId(
        long manifestId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetActiveByManifestIdAsync(tenantId, manifestId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ReleaseBundleResponse>.Fail(ErrorCodes.NotFound, "Active ReleaseBundle not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ReleaseBundleResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
