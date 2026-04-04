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
[Route("api/v2/coze-mappings")]
[Authorize]
[PlatformOnly]
public sealed class CozeMappingsV2Controller : ControllerBase
{
    private readonly ICozeMappingQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public CozeMappingsV2Controller(
        ICozeMappingQueryService queryService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("overview")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<CozeLayerMappingOverview>>> GetOverview(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetOverviewAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<CozeLayerMappingOverview>.Ok(result, HttpContext.TraceIdentifier));
    }
}
