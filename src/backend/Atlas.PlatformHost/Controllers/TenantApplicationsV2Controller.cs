using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/tenant-applications")]
[Authorize]
public sealed class TenantApplicationsV2Controller : ControllerBase
{
    private readonly ITenantApplicationQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public TenantApplicationsV2Controller(
        ITenantApplicationQueryService queryService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantApplicationListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, status, cancellationToken);
        return Ok(ApiResponse<PagedResult<TenantApplicationListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<TenantApplicationDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<TenantApplicationDetail>.Fail(ErrorCodes.NotFound, "Tenant application not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TenantApplicationDetail>.Ok(result, HttpContext.TraceIdentifier));
    }
}
