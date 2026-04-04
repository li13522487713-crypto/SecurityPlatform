using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/migration-governance")]
[Authorize]
[PlatformOnly]
public sealed class MigrationGovernanceV2Controller : ControllerBase
{
    private readonly MigrationGovernanceMetricsStore _metricsStore;

    public MigrationGovernanceV2Controller(MigrationGovernanceMetricsStore metricsStore)
    {
        _metricsStore = metricsStore;
    }

    [HttpGet("overview")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public ActionResult<ApiResponse<MigrationGovernanceOverview>> GetOverview()
    {
        var snapshot = _metricsStore.GetSnapshot();
        return Ok(ApiResponse<MigrationGovernanceOverview>.Ok(snapshot, HttpContext.TraceIdentifier));
    }
}
