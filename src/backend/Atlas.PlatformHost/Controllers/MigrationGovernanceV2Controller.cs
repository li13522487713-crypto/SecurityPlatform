using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/migration-governance")]
[Authorize]
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
