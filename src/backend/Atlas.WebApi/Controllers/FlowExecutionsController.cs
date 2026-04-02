using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/flow-executions")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class FlowExecutionsController : ControllerBase
{
    private readonly IFlowExecutionQueryService _queryService;
    private readonly IFlowExecutionCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;

    public FlowExecutionsController(
        IFlowExecutionQueryService queryService,
        IFlowExecutionCommandService commandService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FlowExecutionListItem>>>> GetExecutions(
        [FromQuery] PagedRequest request,
        [FromQuery] long? flowDefinitionId,
        [FromQuery] ExecutionStatus? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryExecutionsAsync(flowDefinitionId, request, status, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<FlowExecutionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<FlowExecutionResponse>>> GetExecution(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetExecutionByIdAsync(id, tenantId, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<FlowExecutionResponse>.Fail(ErrorCodes.NotFound, "流程执行不存在", HttpContext.TraceIdentifier));
        return Ok(ApiResponse<FlowExecutionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/node-runs")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NodeRunResponse>>>> GetNodeRuns(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var execution = await _queryService.GetExecutionByIdAsync(id, tenantId, cancellationToken);
        if (execution is null)
            return NotFound(ApiResponse<IReadOnlyList<NodeRunResponse>>.Fail(ErrorCodes.NotFound, "流程执行不存在", HttpContext.TraceIdentifier));
        var runs = await _queryService.GetNodeRunsAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NodeRunResponse>>.Ok(runs, HttpContext.TraceIdentifier));
    }

    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<object>>> Trigger(
        [FromBody] FlowExecutionTriggerRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = User.FindFirst("sub")?.Value ?? "system";
        var executionId = await _commandService.TriggerAsync(request, tenantId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ExecutionId = executionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.CancelAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/pause")]
    public async Task<ActionResult<ApiResponse<object>>> Pause(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.PauseAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/resume")]
    public async Task<ActionResult<ApiResponse<object>>> Resume(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.ResumeAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/retry")]
    public async Task<ActionResult<ApiResponse<object>>> Retry(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = User.FindFirst("sub")?.Value ?? "system";
        var newExecutionId = await _commandService.RetryAsync(id, tenantId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ExecutionId = newExecutionId.ToString() }, HttpContext.TraceIdentifier));
    }
}
