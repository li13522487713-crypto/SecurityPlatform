using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// V2 工作流 API —— Coze 风格 DAG 引擎，与 V1 (<see cref="AiWorkflowsController"/>) 并行。
/// </summary>
[ApiController]
[Route("api/v2/workflows")]
public sealed class WorkflowV2Controller : ControllerBase
{
    private readonly IWorkflowV2CommandService _commandService;
    private readonly IWorkflowV2QueryService _queryService;
    private readonly IWorkflowV2ExecutionService _executionService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<WorkflowV2CreateRequest> _createValidator;
    private readonly IValidator<WorkflowV2SaveDraftRequest> _saveDraftValidator;
    private readonly IValidator<WorkflowV2UpdateMetaRequest> _updateMetaValidator;
    private readonly IValidator<WorkflowV2PublishRequest> _publishValidator;
    private readonly IValidator<WorkflowV2RunRequest> _runValidator;
    private readonly IValidator<WorkflowV2NodeDebugRequest> _nodeDebugValidator;

    public WorkflowV2Controller(
        IWorkflowV2CommandService commandService,
        IWorkflowV2QueryService queryService,
        IWorkflowV2ExecutionService executionService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<WorkflowV2CreateRequest> createValidator,
        IValidator<WorkflowV2SaveDraftRequest> saveDraftValidator,
        IValidator<WorkflowV2UpdateMetaRequest> updateMetaValidator,
        IValidator<WorkflowV2PublishRequest> publishValidator,
        IValidator<WorkflowV2RunRequest> runValidator,
        IValidator<WorkflowV2NodeDebugRequest> nodeDebugValidator)
    {
        _commandService = commandService;
        _queryService = queryService;
        _executionService = executionService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _saveDraftValidator = saveDraftValidator;
        _updateMetaValidator = updateMetaValidator;
        _publishValidator = publishValidator;
        _runValidator = runValidator;
        _nodeDebugValidator = nodeDebugValidator;
    }

    // ── 写操作 ──────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] WorkflowV2CreateRequest request, CancellationToken cancellationToken)
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
        long id, [FromBody] WorkflowV2SaveDraftRequest request, CancellationToken cancellationToken)
    {
        _saveDraftValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.SaveDraftAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/meta")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMeta(
        long id, [FromBody] WorkflowV2UpdateMetaRequest request, CancellationToken cancellationToken)
    {
        _updateMetaValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateMetaAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        long id, [FromBody] WorkflowV2PublishRequest request, CancellationToken cancellationToken)
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
    public async Task<ActionResult<ApiResponse<PagedResult<WorkflowV2ListItem>>>> List(
        [FromQuery] PagedRequest request, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ListAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkflowV2ListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2DetailDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2DetailDto>.Fail(ErrorCodes.NotFound, "工作流不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2DetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowV2VersionDto>>>> ListVersions(
        long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ListVersionsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkflowV2VersionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-types")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowV2NodeTypeDto>>>> GetNodeTypes(
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetNodeTypesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkflowV2NodeTypeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ── 执行 ──────────────────────────────────────────────

    [HttpPost("{id:long}/run")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<WorkflowV2RunResult>>> Run(
        long id, [FromBody] WorkflowV2RunRequest? request, CancellationToken cancellationToken)
    {
        var safeRequest = request ?? new WorkflowV2RunRequest(null);
        _runValidator.ValidateAndThrow(safeRequest);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _executionService.SyncRunAsync(tenantId, id, userId, safeRequest, cancellationToken);
        return Ok(ApiResponse<WorkflowV2RunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/stream")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task StreamRun(
        long id, [FromBody] WorkflowV2RunRequest? request, CancellationToken cancellationToken)
    {
        var safeRequest = request ?? new WorkflowV2RunRequest(null);
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
        await _executionService.CancelAsync(tenantId, execId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = execId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId:long}/resume")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<object>>> Resume(long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _executionService.ResumeAsync(tenantId, execId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = execId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/process")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2ExecutionDto>>> GetProcess(
        long execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetExecutionProcessAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2ExecutionDto>.Fail(ErrorCodes.NotFound, "执行实例不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2ExecutionDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId:long}/nodes/{nodeKey}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<WorkflowV2NodeExecutionDto>>> GetNodeDetail(
        long execId, string nodeKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetNodeExecutionDetailAsync(tenantId, execId, nodeKey, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<WorkflowV2NodeExecutionDto>.Fail(ErrorCodes.NotFound, "节点执行记录不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<WorkflowV2NodeExecutionDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/debug-node")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowDebug)]
    public async Task<ActionResult<ApiResponse<WorkflowV2RunResult>>> DebugNode(
        long id, [FromBody] WorkflowV2NodeDebugRequest request, CancellationToken cancellationToken)
    {
        _nodeDebugValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _executionService.DebugNodeAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<WorkflowV2RunResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
