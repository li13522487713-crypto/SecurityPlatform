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
using Microsoft.Extensions.Logging;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// V2 工作流 API —— Coze 风格 DAG 引擎，与 V1 (<see cref="AiWorkflowsController"/>) 并行。
/// </summary>
[ApiController]
[Route("api/v2/workflows")]
public sealed class DagWorkflowController : ControllerBase
{
    private readonly IDagWorkflowCommandService _commandService;
    private readonly IDagWorkflowQueryService _queryService;
    private readonly IDagWorkflowExecutionService _executionService;
    private readonly ICanvasValidator _canvasValidator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<DagWorkflowCreateRequest> _createValidator;
    private readonly IValidator<DagWorkflowSaveDraftRequest> _saveDraftValidator;
    private readonly IValidator<DagWorkflowUpdateMetaRequest> _updateMetaValidator;
    private readonly IValidator<DagWorkflowPublishRequest> _publishValidator;
    private readonly IValidator<DagWorkflowRunRequest> _runValidator;
    private readonly IValidator<DagWorkflowNodeDebugRequest> _nodeDebugValidator;
    private readonly ILogger<DagWorkflowController> _logger;

    public DagWorkflowController(
        IDagWorkflowCommandService commandService,
        IDagWorkflowQueryService queryService,
        IDagWorkflowExecutionService executionService,
        ICanvasValidator canvasValidator,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<DagWorkflowCreateRequest> createValidator,
        IValidator<DagWorkflowSaveDraftRequest> saveDraftValidator,
        IValidator<DagWorkflowUpdateMetaRequest> updateMetaValidator,
        IValidator<DagWorkflowPublishRequest> publishValidator,
        IValidator<DagWorkflowRunRequest> runValidator,
        IValidator<DagWorkflowNodeDebugRequest> nodeDebugValidator,
        ILogger<DagWorkflowController> logger)
    {
        _commandService = commandService;
        _queryService = queryService;
        _executionService = executionService;
        _canvasValidator = canvasValidator;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _saveDraftValidator = saveDraftValidator;
        _updateMetaValidator = updateMetaValidator;
        _publishValidator = publishValidator;
        _runValidator = runValidator;
        _nodeDebugValidator = nodeDebugValidator;
        _logger = logger;
    }

    // ── 写操作 ──────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] DagWorkflowCreateRequest request, CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _commandService.CreateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/draft")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SaveDraft(
        long id, [FromBody] DagWorkflowSaveDraftRequest request, CancellationToken cancellationToken)
    {
        _saveDraftValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.SaveDraftAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/meta")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMeta(
        long id, [FromBody] DagWorkflowUpdateMetaRequest request, CancellationToken cancellationToken)
    {
        _updateMetaValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateMetaAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        long id, [FromBody] DagWorkflowPublishRequest request, CancellationToken cancellationToken)
    {
        _publishValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _commandService.PublishAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/copy")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Copy(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var copiedId = await _commandService.CopyAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = copiedId.ToString() }, HttpContext.TraceIdentifier));
    }

    // ── 查询 ──────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DagWorkflowListItem>>>> List(
        [FromQuery] PagedRequest request, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ListAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<DagWorkflowListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("published")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DagWorkflowListItem>>>> ListPublished(
        [FromQuery] PagedRequest request, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ListPublishedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
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
        var result = await _queryService.GetAsync(tenantId, id, cancellationToken, source, versionId);
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
        var result = await _queryService.ListVersionsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DagWorkflowVersionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/dependencies")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowDependencyDto>>> GetDependencies(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetDependenciesAsync(tenantId, id, cancellationToken);
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
        var result = await _queryService.GetVersionDiffAsync(tenantId, id, fromId, toId, cancellationToken);
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
        var result = await _commandService.RollbackToVersionAsync(tenantId, id, versionId, userId, cancellationToken);
        return Ok(ApiResponse<WorkflowVersionRollbackResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-types")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DagWorkflowNodeTypeDto>>>> GetNodeTypes(
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetNodeTypesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DagWorkflowNodeTypeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-templates")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DagWorkflowNodeTemplateDto>>>> GetNodeTemplates(
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetNodeTemplatesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DagWorkflowNodeTemplateDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ── 执行 ──────────────────────────────────────────────

    [HttpPost("{id:long}/run")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<DagWorkflowRunResult>>> Run(
        long id, [FromBody] DagWorkflowRunRequest? request, CancellationToken cancellationToken)
    {
        var safeRequest = request ?? new DagWorkflowRunRequest(null);
        _runValidator.ValidateAndThrow(safeRequest);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _executionService.SyncRunAsync(tenantId, id, userId, safeRequest, cancellationToken);
        return Ok(ApiResponse<DagWorkflowRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/stream")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task StreamRun(
        long id, [FromBody] DagWorkflowRunRequest? request, CancellationToken cancellationToken)
    {
        var safeRequest = request ?? new DagWorkflowRunRequest(null);
        _runValidator.ValidateAndThrow(safeRequest);

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;

        await foreach (var evt in _executionService.StreamRunAsync(tenantId, id, userId, safeRequest, cancellationToken))
        {
            await Response.WriteAsync($"event: {evt.Event}\ndata: {evt.Data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("executions/{execId:long}/cancel")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var execution = await _queryService.GetExecutionProcessAsync(tenantId, execId, cancellationToken);
        if (execution is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"),
                HttpContext.TraceIdentifier));
        }

        await _executionService.CancelAsync(tenantId, execId, cancellationToken);
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
        await _executionService.ResumeAsync(tenantId, execId, request, cancellationToken);
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
        await foreach (var evt in _executionService.StreamResumeAsync(tenantId, execId, cancellationToken))
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
        var result = await _queryService.GetExecutionProcessAsync(tenantId, execId, cancellationToken);
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
        var result = await _queryService.GetExecutionCheckpointAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowExecutionCheckpointDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowExecutionCheckpointDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId:long}/recover")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<DagWorkflowRunResult>>> Recover(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var checkpoint = await _queryService.GetExecutionCheckpointAsync(tenantId, execId, cancellationToken);
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
        var result = await _executionService.AsyncRunAsync(
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
        var result = await _queryService.GetExecutionDebugViewAsync(tenantId, execId, cancellationToken);
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
        var result = await _queryService.GetNodeExecutionDetailAsync(tenantId, execId, nodeKey, cancellationToken);
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
        _nodeDebugValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _executionService.DebugNodeAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<DagWorkflowRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// API-02: 获取执行 Trace（步骤列表 + 时序信息）。
    /// </summary>
    [HttpGet("executions/{execId:long}/trace")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<DagWorkflowRunTraceDto>>> GetTrace(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var execution = await _queryService.GetExecutionProcessAsync(tenantId, execId, cancellationToken);
        if (execution is null)
        {
            return NotFound(ApiResponse<DagWorkflowRunTraceDto>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"),
                HttpContext.TraceIdentifier));
        }

        var result = await _queryService.GetRunTraceAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DagWorkflowRunTraceDto>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DagWorkflowRunTraceDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// API-03: 校验画布 JSON，返回结构化校验错误列表。
    /// </summary>
    [HttpPost("{id:long}/validate")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<CanvasValidationResult>>> ValidateCanvas(
        long id,
        [FromBody] DagWorkflowValidateRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetAsync(tenantId, id, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<CanvasValidationResult>.Fail(
                WorkflowErrorCodes.WorkflowNotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowDefNotFound"),
                HttpContext.TraceIdentifier));
        }

        var canvasJson = ResolveCanvasJsonForValidation(detail.CanvasJson, request);
        var result = _canvasValidator.ValidateCanvas(canvasJson);
        _logger.LogInformation(
            "ValidateCanvas: WorkflowId={WorkflowId} IsValid={IsValid} ErrorCount={ErrorCount}",
            id, result.IsValid, result.Errors.Count);

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
