using Atlas.Application.Workflow.Abstractions;
using Atlas.Application.Workflow.Models;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 工作流管理控制器
/// </summary>
[ApiController]
[Route("api/v1/workflows")]
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
    [Authorize(Policy = PermissionPolicies.WorkflowView)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowView)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowDesign)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowView)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowView)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowDesign)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowDesign)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowDesign)]
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
    [Authorize(Policy = PermissionPolicies.WorkflowDesign)]
    public async Task<ActionResult<ApiResponse<object>>> PublishEvent(
        [FromBody] PublishEventRequest request,
        CancellationToken cancellationToken)
    {
        await _commandService.PublishEventAsync(request, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 从 JSON 定义注册动态工作流
    /// </summary>
    [HttpPost("definitions")]
    [Authorize(Policy = PermissionPolicies.WorkflowDesign)]
    public async Task<ActionResult<ApiResponse<object>>> RegisterDynamicWorkflow(
        [FromBody] RegisterWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        await _commandService.RegisterWorkflowFromJsonAsync(request, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { Success = true, WorkflowId = request.WorkflowId, Version = request.Version }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 获取工作流执行指针详情（步骤级监控）
    /// </summary>
    [HttpGet("instances/{instanceId}/pointers")]
    [Authorize(Policy = PermissionPolicies.WorkflowView)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ExecutionPointerResponse>>>> GetExecutionPointers(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var pointers = await _queryService.GetExecutionPointersAsync(instanceId, cancellationToken);
        var payload = ApiResponse<IEnumerable<ExecutionPointerResponse>>.Ok(pointers, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    /// <summary>
    /// 获取所有可用的步骤类型
    /// </summary>
    [HttpGet("step-types")]
    [Authorize(Policy = PermissionPolicies.WorkflowView)]
    public ActionResult<ApiResponse<IEnumerable<StepTypeMetadata>>> GetStepTypes()
    {
        var stepTypes = new List<StepTypeMetadata>
        {
            new()
            {
                Type = "Delay",
                Label = "延迟",
                Category = "时间控制",
                Color = "#faad14",
                Icon = "clock-circle",
                Parameters = new List<StepParameter>
                {
                    new() { Name = "Period", Type = "timespan", Required = true, Description = "延迟时长（格式：HH:mm:ss）" }
                }
            },
            new()
            {
                Type = "If",
                Label = "条件判断",
                Category = "控制流",
                Color = "#13c2c2",
                Icon = "branches",
                Parameters = new List<StepParameter>
                {
                    new() { Name = "Condition", Type = "bool", Required = true, Description = "条件表达式" }
                }
            },
            new()
            {
                Type = "While",
                Label = "循环",
                Category = "控制流",
                Color = "#722ed1",
                Icon = "reload",
                Parameters = new List<StepParameter>
                {
                    new() { Name = "Condition", Type = "bool", Required = true, Description = "循环条件" }
                }
            },
            new()
            {
                Type = "Foreach",
                Label = "遍历",
                Category = "控制流",
                Color = "#eb2f96",
                Icon = "unordered-list",
                Parameters = new List<StepParameter>
                {
                    new() { Name = "Collection", Type = "array", Required = true, Description = "集合数据" },
                    new() { Name = "RunParallel", Type = "bool", Required = false, DefaultValue = "true", Description = "是否并行执行" }
                }
            },
            new()
            {
                Type = "Decide",
                Label = "分支决策",
                Category = "控制流",
                Color = "#52c41a",
                Icon = "fork",
                Parameters = new List<StepParameter>()
            },
            new()
            {
                Type = "WaitFor",
                Label = "等待事件",
                Category = "时间控制",
                Color = "#1890ff",
                Icon = "hourglass",
                Parameters = new List<StepParameter>
                {
                    new() { Name = "EventName", Type = "string", Required = true, Description = "事件名称" },
                    new() { Name = "EventKey", Type = "string", Required = false, Description = "事件键（用于关联特定事件）" },
                    new() { Name = "EffectiveDate", Type = "datetime", Required = false, Description = "生效时间" }
                }
            },
            new()
            {
                Type = "Sequence",
                Label = "顺序容器",
                Category = "容器",
                Color = "#595959",
                Icon = "ordered-list",
                Parameters = new List<StepParameter>()
            },
            new()
            {
                Type = "Recur",
                Label = "重复执行",
                Category = "控制流",
                Color = "#fa541c",
                Icon = "sync",
                Parameters = new List<StepParameter>
                {
                    new() { Name = "Interval", Type = "timespan", Required = true, Description = "执行间隔（格式：HH:mm:ss）" },
                    new() { Name = "StopCondition", Type = "bool", Required = false, Description = "停止条件" }
                }
            },
            new()
            {
                Type = "ApprovalStep",
                Label = "审批步骤",
                Category = "审批",
                Color = "#2f54eb",
                Icon = "audit",
                Parameters = new List<StepParameter>
                {
                    new() { Name = "EventName", Type = "string", Required = false, DefaultValue = "ApprovalDecision", Description = "审批事件名称" },
                    new() { Name = "EventKey", Type = "string", Required = true, Description = "审批业务键（建议使用审批实例或业务主键）" },
                    new() { Name = "EffectiveDate", Type = "datetime", Required = false, Description = "等待生效时间" }
                },
                Supported = true
            }
        };

        var payload = ApiResponse<IEnumerable<StepTypeMetadata>>.Ok(stepTypes, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}

