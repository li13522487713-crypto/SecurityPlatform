using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/tenant-app-instances")]
[Authorize]
public sealed class TenantAppInstancesV2Controller : ControllerBase
{
    private readonly ITenantAppInstanceQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public TenantAppInstancesV2Controller(
        ITenantAppInstanceQueryService queryService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantAppInstanceListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<TenantAppInstanceListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<TenantAppInstanceDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<TenantAppInstanceDetail>.Fail(ErrorCodes.NotFound, "Tenant app instance not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TenantAppInstanceDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("data-source-bindings")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantAppDataSourceBinding>>>> GetDataSourceBindings(
        [FromQuery] long[]? appIds,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        IReadOnlyCollection<long>? appInstanceIds = appIds is { Length: > 0 } ? appIds : null;
        var result = await _queryService.GetDataSourceBindingsAsync(tenantId, appInstanceIds, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TenantAppDataSourceBinding>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
