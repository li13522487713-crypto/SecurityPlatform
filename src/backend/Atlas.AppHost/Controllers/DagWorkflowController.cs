using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Authorization;
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
public sealed class DagWorkflowController : ControllerBase
{
    private const string ResourceType = "workflow";

    private readonly IServiceProvider _serviceProvider;
    private readonly IDagWorkflowExecutionService _executionService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IResourceWriteGate _writeGate;
    private readonly ILogger<DagWorkflowController> _logger;

    public DagWorkflowController(
        IServiceProvider serviceProvider,
        IDagWorkflowExecutionService executionService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IResourceWriteGate writeGate,
        ILogger<DagWorkflowController> logger)
    {
        _serviceProvider = serviceProvider;
        _executionService = executionService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _writeGate = writeGate;
        _logger = logger;
    }

    private IDagWorkflowCommandService GetCommandService()
    {
        return _serviceProvider.GetRequiredService<IDagWorkflowCommandService>();
    }

    private IDagWorkflowQueryService GetQueryService()
    {
        return _serviceProvider.GetRequiredService<IDagWorkflowQueryService>();
    }

    private IDagWorkflowExecutionService GetExecutionService()
    {
        return _executionService;
    }

    private IValidator<TRequest> GetValidator<TRequest>()
    {
        return _serviceProvider.GetRequiredService<IValidator<TRequest>>();
    }

    private ICanvasValidator GetCanvasValidator()
    {
        return _serviceProvider.GetRequiredService<ICanvasValidator>();
    }

    // ── 写操作 ──────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] DagWorkflowCreateRequest request, CancellationToken cancellationToken)
    {
        GetValidator<DagWorkflowCreateRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await GetCommandService().CreateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/draft")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SaveDraft(
        long id, [FromBody] DagWorkflowSaveDraftRequest request, CancellationToken cancellationToken)
    {
        GetValidator<DagWorkflowSaveDraftRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "edit", cancellationToken);
        await GetCommandService().SaveDraftAsync(tenantId, id, request, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/meta")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMeta(
        long id, [FromBody] DagWorkflowUpdateMetaRequest request, CancellationToken cancellationToken)
    {
        GetValidator<DagWorkflowUpdateMetaRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "edit", cancellationToken);
        await GetCommandService().UpdateMetaAsync(tenantId, id, request, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        long id, [FromBody] DagWorkflowPublishRequest request, CancellationToken cancellationToken)
    {
        GetValidator<DagWorkflowPublishRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "publish", cancellationToken);
        await GetCommandService().PublishAsync(tenantId, id, userId, request, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "delete", cancellationToken);
        await GetCommandService().DeleteAsync(tenantId, id, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/copy")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Copy(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "view", cancellationToken);
        var copiedId = await GetCommandService().CopyAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = copiedId.ToString() }, HttpContext.TraceIdentifier));
    }

    // ── 查询 ──────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DagWorkflowListItem>>>> List(
        [FromQuery] PagedRequest request, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().ListAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<DagWorkflowListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("published")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DagWorkflowListItem>>>> ListPublished(
        [FromQuery] PagedRequest request, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().ListPublishedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<DagWorkflowListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowDetailDto>>> GetById(
        long id,
        [FromQuery] string? source,
        [FromQuery] long? versionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetAsync(tenantId, id, cancellationToken, source, versionId);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowDetailDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowDefNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DagWorkflowVersionDto>>>> ListVersions(
        long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().ListVersionsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DagWorkflowVersionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/dependencies")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowDependencyDto>>> GetDependencies(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetDependenciesAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowDependencyDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowDefNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowDependencyDto>.Ok(result, HttpContext.TraceIdentifier));
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
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "edit", cancellationToken);
        var result = await GetCommandService().RollbackToVersionAsync(tenantId, id, versionId, userId, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<WorkflowVersionRollbackResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-types")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DagWorkflowNodeTypeDto>>>> GetNodeTypes(
        CancellationToken cancellationToken)
    {
        var result = await GetQueryService().GetNodeTypesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DagWorkflowNodeTypeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-templates")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DagWorkflowNodeTemplateDto>>>> GetNodeTemplates(
        CancellationToken cancellationToken)
    {
        var result = await GetQueryService().GetNodeTemplatesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DagWorkflowNodeTemplateDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ── 执行 ──────────────────────────────────────────────

    [HttpPost("{id:long}/run")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<DagWorkflowRunResult>>> Run(
        long id, [FromBody] DagWorkflowRunRequest? request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DagWorkflow Run received: WorkflowId={WorkflowId}", id);
        var safeRequest = request ?? new DagWorkflowRunRequest(null);
        GetValidator<DagWorkflowRunRequest>().ValidateAndThrow(safeRequest);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "execute", cancellationToken);
        var executionService = GetExecutionService();
        _logger.LogInformation("DagWorkflow Run invoking execution service: WorkflowId={WorkflowId}", id);
        var result = await executionService.SyncRunAsync(tenantId, id, userId, safeRequest, cancellationToken);
        _logger.LogInformation(
            "DagWorkflow Run completed: WorkflowId={WorkflowId} ExecutionId={ExecutionId} Status={Status}",
            id,
            result.ExecutionId,
            result.Status);
        return Ok(ApiResponse<DagWorkflowRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/stream")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task StreamRun(
        long id, [FromBody] DagWorkflowRunRequest? request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DagWorkflow StreamRun received: WorkflowId={WorkflowId}", id);
        var safeRequest = request ?? new DagWorkflowRunRequest(null);
        GetValidator<DagWorkflowRunRequest>().ValidateAndThrow(safeRequest);

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "execute", cancellationToken);
        var executionService = GetExecutionService();
        _logger.LogInformation("DagWorkflow StreamRun start enumerating events: WorkflowId={WorkflowId}", id);

        await foreach (var evt in executionService.StreamRunAsync(tenantId, id, userId, safeRequest, cancellationToken))
        {
            await Response.WriteAsync($"event: {evt.Event}\ndata: {evt.Data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        _logger.LogInformation("DagWorkflow StreamRun completed: WorkflowId={WorkflowId}", id);
    }

    [HttpPost("executions/{execId:long}/cancel")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var execution = await GetQueryService().GetExecutionProcessAsync(tenantId, execId, cancellationToken);
        if (execution is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"),
                HttpContext.TraceIdentifier));
        }

        var executionService = GetExecutionService();
        await executionService.CancelAsync(tenantId, execId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = execId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId:long}/resume")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<object>>> Resume(
        long execId,
        [FromBody] DagWorkflowResumeRequest? request,
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
    public async Task<ActionResult<ApiResponse<DagWorkflowExecutionDto>>> GetProcess(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetExecutionProcessAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowExecutionDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowExecutionDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/checkpoint")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowExecutionCheckpointDto>>> GetCheckpoint(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetExecutionCheckpointAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowExecutionCheckpointDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowExecutionCheckpointDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/trace")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowRunTraceDto>>> GetTrace(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetRunTraceAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowRunTraceDto>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowRunTraceDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId:long}/recover")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<DagWorkflowRunResult>>> Recover(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var checkpoint = await GetQueryService().GetExecutionCheckpointAsync(tenantId, execId, cancellationToken);
        if (checkpoint is null)
        {
            return NotFound(ApiResponse<DagWorkflowRunResult>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        if (checkpoint.Status is not (ExecutionStatus.Failed or ExecutionStatus.Cancelled or ExecutionStatus.Interrupted))
        {
            return BadRequest(ApiResponse<DagWorkflowRunResult>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "DagWorkflowResumeInvalidState"),
                HttpContext.TraceIdentifier));
        }

        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var executionService = GetExecutionService();
        var result = await executionService.AsyncRunAsync(
            tenantId,
            checkpoint.WorkflowId,
            userId,
            new DagWorkflowRunRequest(checkpoint.InputsJson),
            cancellationToken);
        return Ok(ApiResponse<DagWorkflowRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/debug-view")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowExecutionDebugViewDto>>> GetDebugView(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetExecutionDebugViewAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowExecutionDebugViewDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowExecutionDebugViewDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/nodes/{nodeKey}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowNodeExecutionDto>>> GetNodeDetail(
        long execId, string nodeKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await GetQueryService().GetNodeExecutionDetailAsync(tenantId, execId, nodeKey, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowNodeExecutionDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowNodeExecutionNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowNodeExecutionDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/debug-node")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowDebug)]
    public async Task<ActionResult<ApiResponse<DagWorkflowRunResult>>> DebugNode(
        long id, [FromBody] DagWorkflowNodeDebugRequest request, CancellationToken cancellationToken)
    {
        GetValidator<DagWorkflowNodeDebugRequest>().ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var executionService = GetExecutionService();
        var result = await executionService.DebugNodeAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<DagWorkflowRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/validate")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<CanvasValidationResult>>> ValidateCanvas(
        long id,
        [FromBody] DagWorkflowValidateRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await GetQueryService().GetAsync(tenantId, id, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<CanvasValidationResult>.Fail(
                WorkflowErrorCodes.WorkflowNotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowDefNotFound"),
                HttpContext.TraceIdentifier));
        }

        var canvasJson = ResolveCanvasJsonForValidation(detail.CanvasJson, request);
        var result = GetCanvasValidator().ValidateCanvas(canvasJson);
        _logger.LogInformation(
            "ValidateCanvas: WorkflowId={WorkflowId} IsValid={IsValid} ErrorCount={ErrorCount}",
            id,
            result.IsValid,
            result.Errors.Count);

        return Ok(ApiResponse<CanvasValidationResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    private static string ResolveCanvasJsonForValidation(
        string persistedCanvasJson,
        DagWorkflowValidateRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(request?.CanvasJson))
        {
            return request.CanvasJson;
        }

        if (request?.Canvas is not null)
        {
            return JsonSerializer.Serialize(request.Canvas);
        }

        return persistedCanvasJson;
    }
}
