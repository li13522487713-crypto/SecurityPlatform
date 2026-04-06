using Atlas.Application.Visualization.Abstractions;
using Atlas.Application.Visualization.Models;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 可视化指标控制器（PlatformHost 侧，供工作台首页调用）
/// </summary>
[ApiController]
[Route("api/v1/visualization")]
public sealed class VisualizationController : ControllerBase
{
    private readonly IVisualizationQueryService _queryService;

    public VisualizationController(IVisualizationQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("metrics")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<VisualizationMetricsResponse>>> GetMetrics(
        [FromQuery] string? department,
        [FromQuery] string? flowType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var filter = new VisualizationFilterRequest
        {
            Department = department,
            FlowType = flowType,
            From = from,
            To = to
        };

        var result = await _queryService.GetMetricsAsync(filter, cancellationToken);
        return Ok(ApiResponse<VisualizationMetricsResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
