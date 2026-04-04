using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/open-api-stats")]
[Authorize]
public sealed class OpenApiStatsController : ControllerBase
{
    private readonly IOpenApiCallLogService _callLogService;
    private readonly ITenantProvider _tenantProvider;

    public OpenApiStatsController(
        IOpenApiCallLogService callLogService,
        ITenantProvider tenantProvider)
    {
        _callLogService = callLogService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<ActionResult<ApiResponse<OpenApiCallStatsSummary>>> GetSummary(
        [FromQuery] long? projectId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var summary = await _callLogService.GetSummaryAsync(
            tenantId,
            projectId,
            fromUtc,
            toUtc,
            cancellationToken);
        return Ok(ApiResponse<OpenApiCallStatsSummary>.Ok(summary, HttpContext.TraceIdentifier));
    }
}
