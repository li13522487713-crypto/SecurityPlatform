using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/release-center/releases")]
[Authorize]
public sealed class ReleaseCenterV2Controller : ControllerBase
{
    private readonly IReleaseCenterQueryService _queryService;
    private readonly IAppReleaseCommandService _appReleaseCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ReleaseCenterV2Controller(
        IReleaseCenterQueryService queryService,
        IAppReleaseCommandService appReleaseCommandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _appReleaseCommandService = appReleaseCommandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReleaseCenterListItem>>>> Query(
        [FromQuery] PagedRequest request,
        [FromQuery] string? status,
        [FromQuery] string? appKey,
        [FromQuery] long? manifestId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, status, appKey, manifestId, cancellationToken);
        return Ok(ApiResponse<PagedResult<ReleaseCenterListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ReleaseCenterDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ReleaseCenterDetail>.Fail(ErrorCodes.NotFound, "Release not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ReleaseCenterDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/diff")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ReleaseDiffSummary>>> GetDiff(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetDiffAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ReleaseDiffSummary>.Fail(ErrorCodes.NotFound, "Release not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ReleaseDiffSummary>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/impact")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ReleaseImpactSummary>>> GetImpact(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetImpactAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ReleaseImpactSummary>.Fail(ErrorCodes.NotFound, "Release not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ReleaseImpactSummary>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<ReleaseRollbackResult>>> Rollback(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<ReleaseRollbackResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var release = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (release is null)
        {
            return NotFound(ApiResponse<ReleaseRollbackResult>.Fail(ErrorCodes.NotFound, "Release not found.", HttpContext.TraceIdentifier));
        }

        if (!long.TryParse(release.ApplicationCatalogId, out var catalogId))
        {
            return BadRequest(ApiResponse<ReleaseRollbackResult>.Fail(ErrorCodes.ValidationError, "Invalid release catalog reference.", HttpContext.TraceIdentifier));
        }

        var rollbackResult = await _appReleaseCommandService.RollbackAsync(tenantId, currentUser.UserId, catalogId, id, cancellationToken);
        return Ok(ApiResponse<ReleaseRollbackResult>.Ok(rollbackResult, HttpContext.TraceIdentifier));
    }

    [HttpGet("precheck/{manifestId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ReleasePreCheckResult>>> PreCheck(
        long manifestId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _appReleaseCommandService.PreCheckAsync(tenantId, manifestId, cancellationToken);
        return Ok(ApiResponse<ReleasePreCheckResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
