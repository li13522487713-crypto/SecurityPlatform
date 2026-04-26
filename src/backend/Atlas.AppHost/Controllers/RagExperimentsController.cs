using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/rag-experiments")]
[Authorize]
public sealed class RagExperimentsController : ControllerBase
{
    private readonly IRagExperimentService _ragExperimentService;
    private readonly ITenantProvider _tenantProvider;

    public RagExperimentsController(
        IRagExperimentService ragExperimentService,
        ITenantProvider tenantProvider)
    {
        _ragExperimentService = ragExperimentService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("runs")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RagExperimentRunDto>>>> GetRuns(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _ragExperimentService.GetRecentRunsAsync(tenantId, limit, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RagExperimentRunDto>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpGet("comparisons")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RagShadowComparisonDto>>>> GetComparisons(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _ragExperimentService.GetRecentComparisonsAsync(tenantId, limit, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RagShadowComparisonDto>>.Ok(rows, HttpContext.TraceIdentifier));
    }
}
