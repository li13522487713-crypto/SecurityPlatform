using Atlas.Application.Visualization.Abstractions;
using Atlas.Application.Visualization.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 流程可视化中心控制器（骨架版）
/// </summary>
[ApiController]
[Route("visualization")]
[Authorize]
public sealed class VisualizationController : ControllerBase
{
    private readonly IVisualizationQueryService _queryService;

    public VisualizationController(IVisualizationQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<VisualizationOverviewResponse>>> GetOverview(
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

        var overview = await _queryService.GetOverviewAsync(filter, cancellationToken);
        return Ok(ApiResponse<VisualizationOverviewResponse>.Ok(overview, HttpContext.TraceIdentifier));
    }

    [HttpGet("processes")]
    public async Task<ActionResult<ApiResponse<PagedResult<VisualizationProcessSummary>>>> GetProcesses(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetProcessesAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<VisualizationProcessSummary>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("instances")]
    public async Task<ActionResult<ApiResponse<PagedResult<VisualizationInstanceSummary>>>> GetInstances(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetInstancesAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<VisualizationInstanceSummary>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("processes/validate")]
    public async Task<ActionResult<ApiResponse<VisualizationValidationResponse>>> ValidateProcess(
        [FromBody] ValidateVisualizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.ValidateAsync(request, cancellationToken);
        return Ok(ApiResponse<VisualizationValidationResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("processes/publish")]
    public async Task<ActionResult<ApiResponse<VisualizationPublishResponse>>> PublishProcess(
        [FromBody] PublishVisualizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.PublishAsync(request, cancellationToken);
        return Ok(ApiResponse<VisualizationPublishResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
