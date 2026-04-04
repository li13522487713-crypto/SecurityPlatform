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
[Route("api/v2/runtime-contexts")]
[Authorize]
[AppRuntimeOnly]
public sealed class RuntimeContextsV2Controller : ControllerBase
{
    private readonly IRuntimeContextQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public RuntimeContextsV2Controller(
        IRuntimeContextQueryService queryService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RuntimeContextListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? appKey,
        [FromQuery] string? pageKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, appKey, pageKey, cancellationToken);
        return Ok(ApiResponse<PagedResult<RuntimeContextListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<RuntimeContextDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<RuntimeContextDetail>.Fail(ErrorCodes.NotFound, "Runtime context not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<RuntimeContextDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{appKey}/{pageKey}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<RuntimeContextDetail>>> GetByRoute(
        string appKey,
        string pageKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByRouteAsync(tenantId, appKey, pageKey, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<RuntimeContextDetail>.Fail(ErrorCodes.NotFound, "Runtime context not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<RuntimeContextDetail>.Ok(result, HttpContext.TraceIdentifier));
    }
}
