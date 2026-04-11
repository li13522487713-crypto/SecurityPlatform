using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// V2 工作流 API —— Coze 风格 DAG 引擎，与 V1 (<see cref="AiWorkflowsController"/>) 并行。
/// </summary>
[ApiController]
[Route("api/v2/workflows")]
public sealed class WorkflowV2Controller : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowV2ExecutionService _executionService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<WorkflowV2Controller> _logger;

    public WorkflowV2Controller(
        IServiceProvider serviceProvider,
        IWorkflowV2ExecutionService executionService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        ILogger<WorkflowV2Controller> logger)
    {
        _serviceProvider = serviceProvider;
        _executionService = executionService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }

    private IWorkflowV2CommandService GetCommandService()
    {
        return _serviceProvider.GetRequiredService<IWorkflowV2CommandService>();
    }

    private IWorkflowV2QueryService GetQueryService()
    {
        return _serviceProvider.GetRequiredService<IWorkflowV2QueryService>();
    }

    private IWorkflowV2ExecutionService GetExecutionService()
    {
        return _executionService;
    }

    private IValidator<TRequest> GetValidator<TRequest>()
    {
        return _serviceProvider.GetRequiredService<IValidator<TRequest>>();
    }

    // ── 写操作 ──────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] WorkflowV2CreateRequest request, CancellationToken cancellationToken)
    {
        GetValidator<WorkflowV2CreateRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await GetCommandService().CreateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/draft")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SaveDraft(
        long id, [FromBody] WorkflowV2SaveDraftRequest request, CancellationToken cancellationToken)
    {
        GetValidator<WorkflowV2SaveDraftRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await GetCommandService().SaveDraftAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/meta")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMeta(
        long id, [FromBody] WorkflowV2UpdateMetaRequest request, CancellationToken cancellationToken)
    {
        GetValidator<WorkflowV2UpdateMetaRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await GetCommandService().UpdateMetaAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        long id, [FromBody] WorkflowV2PublishRequest request, CancellationToken cancellationToken)
    {
        GetValidator<WorkflowV2PublishRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await GetCommandService().PublishAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await GetCommandService().DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/copy")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Copy(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var copiedId = await GetCommandService().CopyAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = copiedId.ToString() }, HttpContext.TraceIdentifier));
    }

    // ── 查询 ──────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkflowV2ListItem>>>> List(
        [FromQuery] PagedRequest request, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().ListAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkflowV2ListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("published")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkflowV2ListItem>>>> ListPublished(
        [FromQuery] PagedRequest request, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().ListPublishedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkflowV2ListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2DetailDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2DetailDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowDefNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2DetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowV2VersionDto>>>> ListVersions(
        long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().ListVersionsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkflowV2VersionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions/{fromId:long}/diff/{toId:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowVersionDiff>>> GetVersionDiff(
        long id, long fromId, long toId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetVersionDiffAsync(tenantId, id, fromId, toId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowVersionDiff>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowVersionNotInWorkflow"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowVersionDiff>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/versions/{versionId:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<WorkflowVersionRollbackResult>>> Rollback(
        long id, long versionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await GetCommandService().RollbackToVersionAsync(tenantId, id, versionId, userId, cancellationToken);
        return Ok(ApiResponse<WorkflowVersionRollbackResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-types")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowV2NodeTypeDto>>>> GetNodeTypes(
        CancellationToken cancellationToken)
    {
        var result = await GetQueryService().GetNodeTypesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkflowV2NodeTypeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-templates")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowV2NodeTemplateDto>>>> GetNodeTemplates(
        CancellationToken cancellationToken)
    {
        var result = await GetQueryService().GetNodeTemplatesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkflowV2NodeTemplateDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ── 执行 ──────────────────────────────────────────────

    [HttpPost("{id:long}/run")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<WorkflowV2RunResult>>> Run(
        long id, [FromBody] WorkflowV2RunRequest? request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[diag] AppHost Run entered workflowId={id}");
        _logger.LogWarning("WorkflowV2 Run received: WorkflowId={WorkflowId}", id);
        var safeRequest = request ?? new WorkflowV2RunRequest(null);
        GetValidator<WorkflowV2RunRequest>().ValidateAndThrow(safeRequest);
        Console.WriteLine($"[diag] AppHost Run validated workflowId={id}");
        _logger.LogWarning("WorkflowV2 Run validated: WorkflowId={WorkflowId}", id);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var executionService = GetExecutionService();
        Console.WriteLine($"[diag] AppHost Run calling SyncRunAsync workflowId={id}");
        _logger.LogWarning("WorkflowV2 Run invoking execution service: WorkflowId={WorkflowId}", id);
        var result = await executionService.SyncRunAsync(tenantId, id, userId, safeRequest, cancellationToken);
        Console.WriteLine($"[diag] AppHost Run returned executionId={result.ExecutionId} status={result.Status}");
        _logger.LogWarning(
            "WorkflowV2 Run completed: WorkflowId={WorkflowId} ExecutionId={ExecutionId} Status={Status} Error={Error}",
            id,
            result.ExecutionId,
            result.Status,
            result.ErrorMessage);
        return Ok(ApiResponse<WorkflowV2RunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/stream")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task StreamRun(
        long id, [FromBody] WorkflowV2RunRequest? request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[diag] AppHost StreamRun entered workflowId={id}");
        _logger.LogWarning("WorkflowV2 StreamRun received: WorkflowId={WorkflowId}", id);
        var safeRequest = request ?? new WorkflowV2RunRequest(null);
        Console.WriteLine($"[diag] AppHost StreamRun before validate workflowId={id}");
        GetValidator<WorkflowV2RunRequest>().ValidateAndThrow(safeRequest);
        Console.WriteLine($"[diag] AppHost StreamRun validated workflowId={id}");

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Console.WriteLine($"[diag] AppHost StreamRun headers-set workflowId={id}");

        Console.WriteLine($"[diag] AppHost StreamRun before tenant workflowId={id}");
        var tenantId = _tenantProvider.GetTenantId();
        Console.WriteLine($"[diag] AppHost StreamRun tenant={tenantId.Value} workflowId={id}");
        Console.WriteLine($"[diag] AppHost StreamRun before user workflowId={id}");
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        Console.WriteLine($"[diag] AppHost StreamRun user={userId} workflowId={id}");
        Console.WriteLine($"[diag] AppHost StreamRun before resolve execution service workflowId={id}");
        var executionService = GetExecutionService();
        Console.WriteLine($"[diag] AppHost StreamRun resolved execution service workflowId={id}");
        Console.WriteLine($"[diag] AppHost StreamRun enumerating workflowId={id}");
        _logger.LogWarning("WorkflowV2 StreamRun start enumerating events: WorkflowId={WorkflowId}", id);

        await foreach (var evt in executionService.StreamRunAsync(tenantId, id, userId, safeRequest, cancellationToken))
        {
            Console.WriteLine($"[diag] AppHost StreamRun event workflowId={id} event={evt.Event}");
            await Response.WriteAsync($"event: {evt.Event}\ndata: {evt.Data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        Console.WriteLine($"[diag] AppHost StreamRun completed workflowId={id}");
    }

    [HttpPost("executions/{execId:long}/cancel")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var executionService = GetExecutionService();
        await executionService.CancelAsync(tenantId, execId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = execId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId:long}/resume")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<object>>> Resume(
        long execId,
        [FromBody] WorkflowV2ResumeRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var executionService = GetExecutionService();
        await executionService.ResumeAsync(tenantId, execId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = execId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId:long}/stream-resume")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task StreamResume(long execId, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();
        var executionService = GetExecutionService();
        await foreach (var evt in executionService.StreamResumeAsync(tenantId, execId, cancellationToken))
        {
            await Response.WriteAsync($"event: {evt.Event}\ndata: {evt.Data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("executions/{execId:long}/process")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2ExecutionDto>>> GetProcess(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetExecutionProcessAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2ExecutionDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2ExecutionDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/checkpoint")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2ExecutionCheckpointDto>>> GetCheckpoint(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetExecutionCheckpointAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2ExecutionCheckpointDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2ExecutionCheckpointDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId:long}/recover")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<WorkflowV2RunResult>>> Recover(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var checkpoint = await GetQueryService().GetExecutionCheckpointAsync(tenantId, execId, cancellationToken);
        if (checkpoint is null)
        {
            return NotFound(ApiResponse<WorkflowV2RunResult>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        if (checkpoint.Status is not (ExecutionStatus.Failed or ExecutionStatus.Cancelled or ExecutionStatus.Interrupted))
        {
            return BadRequest(ApiResponse<WorkflowV2RunResult>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "WorkflowV2ResumeInvalidState"),
                HttpContext.TraceIdentifier));
        }

        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var executionService = GetExecutionService();
        var result = await executionService.AsyncRunAsync(
            tenantId,
            checkpoint.WorkflowId,
            userId,
            new WorkflowV2RunRequest(checkpoint.InputsJson),
            cancellationToken);
        return Ok(ApiResponse<WorkflowV2RunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/debug-view")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2ExecutionDebugViewDto>>> GetDebugView(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetExecutionDebugViewAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2ExecutionDebugViewDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2ExecutionDebugViewDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/nodes/{nodeKey}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2NodeExecutionDto>>> GetNodeDetail(
        long execId, string nodeKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetNodeExecutionDetailAsync(tenantId, execId, nodeKey, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2NodeExecutionDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowNodeExecutionNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2NodeExecutionDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/debug-node")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowDebug)]
    public async Task<ActionResult<ApiResponse<WorkflowV2RunResult>>> DebugNode(
        long id, [FromBody] WorkflowV2NodeDebugRequest request, CancellationToken cancellationToken)
    {
        GetValidator<WorkflowV2NodeDebugRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var executionService = GetExecutionService();
        var result = await executionService.DebugNodeAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<WorkflowV2RunResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
