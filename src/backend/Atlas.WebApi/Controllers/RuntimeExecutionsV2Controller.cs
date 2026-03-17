using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/runtime-executions")]
[Authorize]
public sealed class RuntimeExecutionsV2Controller : ControllerBase
{
    private readonly IRuntimeExecutionQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public RuntimeExecutionsV2Controller(
        IRuntimeExecutionQueryService queryService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RuntimeExecutionListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<RuntimeExecutionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<RuntimeExecutionDetail>.Fail(ErrorCodes.NotFound, "Runtime execution not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<RuntimeExecutionDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/audit-trails")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RuntimeExecutionAuditTrailItem>>>> GetAuditTrails(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAuditTrailsAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<RuntimeExecutionAuditTrailItem>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
