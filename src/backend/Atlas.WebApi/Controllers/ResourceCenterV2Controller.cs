using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/resource-center")]
[Authorize]
public sealed class ResourceCenterV2Controller : ControllerBase
{
    private readonly IResourceCenterQueryService _resourceCenterQueryService;
    private readonly ITenantProvider _tenantProvider;

    public ResourceCenterV2Controller(
        IResourceCenterQueryService resourceCenterQueryService,
        ITenantProvider tenantProvider)
    {
        _resourceCenterQueryService = resourceCenterQueryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("groups")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ResourceCenterGroupItem>>>> GetGroups(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var groups = await _resourceCenterQueryService.GetGroupsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ResourceCenterGroupItem>>.Ok(groups, HttpContext.TraceIdentifier));
    }

    [HttpGet("datasource-consumption")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ResourceCenterDataSourceConsumptionResponse>>> GetDataSourceConsumption(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var response = await _resourceCenterQueryService.GetDataSourceConsumptionAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<ResourceCenterDataSourceConsumptionResponse>.Ok(response, HttpContext.TraceIdentifier));
    }
}
