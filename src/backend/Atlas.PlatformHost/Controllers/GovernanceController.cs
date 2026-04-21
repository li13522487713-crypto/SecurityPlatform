using Atlas.Core.Governance;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/governance")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class GovernanceController : ControllerBase
{
    private readonly ICanaryReleaseService _canaryService;
    private readonly IVersionFreezeService _versionFreezeService;
    private readonly ITenantProvider _tenantProvider;

    public GovernanceController(
        ICanaryReleaseService canaryService,
        IVersionFreezeService versionFreezeService,
        ITenantProvider tenantProvider)
    {
        _canaryService = canaryService;
        _versionFreezeService = versionFreezeService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("canary-releases")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CanaryReleaseInfo>>>> GetCanaryReleases(CancellationToken cancellationToken)
    {
        var list = await _canaryService.ListAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CanaryReleaseInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPut("canary-releases/{featureKey}")]
    public async Task<ActionResult<ApiResponse<object>>> SetCanaryRollout(
        string featureKey,
        [FromBody] SetCanaryRolloutBody body,
        CancellationToken cancellationToken)
    {
        await _canaryService.SetRolloutPercentageAsync(featureKey, body.RolloutPercentage, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpGet("version-freezes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VersionFreezeInfo>>>> GetVersionFreezes(
        [FromQuery] string? resourceType,
        [FromQuery] long? resourceId,
        CancellationToken cancellationToken)
    {
        var list = await _versionFreezeService.QueryFreezesAsync(resourceType, resourceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<VersionFreezeInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost("version-freezes")]
    public async Task<ActionResult<ApiResponse<object>>> FreezeVersion(
        [FromBody] FreezeVersionBody body,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "system";
        await _versionFreezeService.FreezeAsync(body.ResourceType, body.ResourceId, body.Reason, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("version-freezes/{resourceType}/{resourceId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UnfreezeVersion(
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "system";
        await _versionFreezeService.UnfreezeAsync(resourceType, resourceId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

public sealed record SetCanaryRolloutBody(int RolloutPercentage);

public sealed record FreezeVersionBody(string ResourceType, long ResourceId, string Reason);
