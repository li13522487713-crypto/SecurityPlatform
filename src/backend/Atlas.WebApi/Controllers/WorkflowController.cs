using Atlas.Application.Workflow.Abstractions;
using Atlas.Application.Workflow.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 工作流管理控制器
/// </summary>
[ApiController]
[Route("workflows")]
[Authorize]
public sealed class WorkflowController : ControllerBase
{
    private readonly IWorkflowQueryService _queryService;
    private readonly IWorkflowCommandService _commandService;

    public WorkflowController(
        IWorkflowQueryService queryService,
        IWorkflowCommandService commandService)
    {
        _queryService = queryService;
        _commandService = commandService;
    }

    /// <summary>
    /// 获取所有已注册的工作流定义
    /// </summary>
    [HttpGet("definitions")]
    public async Task<ActionResult<ApiResponse<IEnumerable<WorkflowDefinitionResponse>>>> GetDefinitions(
        CancellationToken cancellationToken)
    {
        var definitions = await _queryService.GetAllDefinitionsAsync(cancellationToken);
        var payload = ApiResponse<IEnumerable<WorkflowDefinitionResponse>>.Ok(definitions, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 获取指定工作流定义
    /// </summary>
    [HttpGet("definitions/{workflowId}")]
    public async Task<ActionResult<ApiResponse<WorkflowDefinitionResponse>>> GetDefinition(
        string workflowId,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var definition = await _queryService.GetDefinitionAsync(workflowId, version, cancellationToken);
        if (definition == null)
        {
            var errorPayload = ApiResponse<WorkflowDefinitionResponse>.Fail(
                "NOT_FOUND",
                $"工作流定义不存在: {workflowId}",
                HttpContext.TraceIdentifier);
            return NotFound(errorPayload);
        }

        var payload = ApiResponse<WorkflowDefinitionResponse>.Ok(definition, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 启动工作流实例
    /// </summary>
    [HttpPost("instances")]
    public async Task<ActionResult<ApiResponse<object>>> StartWorkflow(
        [FromBody] StartWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        var instanceId = await _commandService.StartWorkflowAsync(request, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { InstanceId = instanceId }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 获取工作流实例详情
    /// </summary>
    [HttpGet("instances/{instanceId}")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceResponse>>> GetInstance(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var instance = await _queryService.GetWorkflowInstanceAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            var errorPayload = ApiResponse<WorkflowInstanceResponse>.Fail(
                "NOT_FOUND",
                $"工作流实例不存在: {instanceId}",
                HttpContext.TraceIdentifier);
            return NotFound(errorPayload);
        }

        var payload = ApiResponse<WorkflowInstanceResponse>.Ok(instance, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 分页查询工作流实例列表
    /// </summary>
    [HttpGet("instances")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkflowInstanceListItem>>>> GetInstances(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetWorkflowInstancesAsync(request, cancellationToken);
        var payload = ApiResponse<PagedResult<WorkflowInstanceListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 挂起工作流实例
    /// </summary>
    [HttpPost("instances/{instanceId}/suspend")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> SuspendWorkflow(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var success = await _commandService.SuspendWorkflowAsync(instanceId, cancellationToken);
        if (!success)
        {
            var errorPayload = ApiResponse<object>.Fail(
                "OPERATION_FAILED",
                $"挂起工作流失败: {instanceId}",
                HttpContext.TraceIdentifier);
            return BadRequest(errorPayload);
        }

        var payload = ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 恢复工作流实例
    /// </summary>
    [HttpPost("instances/{instanceId}/resume")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> ResumeWorkflow(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var success = await _commandService.ResumeWorkflowAsync(instanceId, cancellationToken);
        if (!success)
        {
            var errorPayload = ApiResponse<object>.Fail(
                "OPERATION_FAILED",
                $"恢复工作流失败: {instanceId}",
                HttpContext.TraceIdentifier);
            return BadRequest(errorPayload);
        }

        var payload = ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 终止工作流实例
    /// </summary>
    [HttpPost("instances/{instanceId}/terminate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> TerminateWorkflow(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var success = await _commandService.TerminateWorkflowAsync(instanceId, cancellationToken);
        if (!success)
        {
            var errorPayload = ApiResponse<object>.Fail(
                "OPERATION_FAILED",
                $"终止工作流失败: {instanceId}",
                HttpContext.TraceIdentifier);
            return BadRequest(errorPayload);
        }

        var payload = ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 发布外部事件
    /// </summary>
    [HttpPost("events")]
    public async Task<ActionResult<ApiResponse<object>>> PublishEvent(
        [FromBody] PublishEventRequest request,
        CancellationToken cancellationToken)
    {
        await _commandService.PublishEventAsync(request, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}
