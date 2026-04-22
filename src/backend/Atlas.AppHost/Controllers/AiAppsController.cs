using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/ai-apps")]
[Authorize]
public sealed class AiAppsController : ControllerBase
{
    private const string ResourceType = "app";

    private readonly IAiAppService _aiAppService;
    private readonly ICozeWorkflowExecutionService _workflowExecutionService;
    private readonly ICozeWorkflowQueryService _workflowQueryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IResourceWriteGate _writeGate;
    private readonly IValidator<AiAppCreateRequest> _createValidator;
    private readonly IValidator<AiAppUpdateRequest> _updateValidator;
    private readonly IValidator<AiAppPublishRequest> _publishValidator;
    private readonly IValidator<AiAppResourceCopyRequest> _resourceCopyValidator;

    public AiAppsController(
        IAiAppService aiAppService,
        ICozeWorkflowExecutionService workflowExecutionService,
        ICozeWorkflowQueryService workflowQueryService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IResourceWriteGate writeGate,
        IValidator<AiAppCreateRequest> createValidator,
        IValidator<AiAppUpdateRequest> updateValidator,
        IValidator<AiAppPublishRequest> publishValidator,
        IValidator<AiAppResourceCopyRequest> resourceCopyValidator)
    {
        _aiAppService = aiAppService;
        _workflowExecutionService = workflowExecutionService;
        _workflowQueryService = workflowQueryService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _writeGate = writeGate;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _publishValidator = publishValidator;
        _resourceCopyValidator = resourceCopyValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiAppListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiAppService.GetPagedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AiAppListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<AiAppDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiAppService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiAppDetail>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "AiAppDetailNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiAppDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiAppCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AiAppCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _aiAppService.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AiAppUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "edit", cancellationToken);
        await _aiAppService.UpdateAsync(tenantId, id, request, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "delete", cancellationToken);
        await _aiAppService.DeleteAsync(tenantId, id, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiAppPublish)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        long id,
        [FromBody] AiAppPublishRequest request,
        CancellationToken cancellationToken)
    {
        _publishValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "publish", cancellationToken);
        await _aiAppService.PublishAsync(tenantId, id, currentUser.UserId, request, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/publish-records")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiAppPublishRecordItem>>>> GetPublishRecords(
        long id,
        [FromQuery] int top = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiAppService.GetPublishRecordsAsync(tenantId, id, top, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiAppPublishRecordItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/conversation-templates")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiAppConversationTemplateListItem>>>> GetConversationTemplates(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiAppService.GetConversationTemplatesAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiAppConversationTemplateListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/conversation-templates")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateConversationTemplate(
        long id,
        [FromBody] AiAppConversationTemplateCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var templateId = await _aiAppService.CreateConversationTemplateAsync(
            tenantId,
            id,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = templateId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/conversation-templates/{templateId:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateConversationTemplate(
        long id,
        long templateId,
        [FromBody] AiAppConversationTemplateUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _aiAppService.UpdateConversationTemplateAsync(
            tenantId,
            id,
            templateId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = templateId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/conversation-templates/{templateId:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteConversationTemplate(
        long id,
        long templateId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _aiAppService.DeleteConversationTemplateAsync(
            tenantId,
            id,
            templateId,
            currentUser.UserId,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = templateId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/version-check")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<AiAppVersionCheckResult>>> CheckVersion(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiAppService.CheckVersionAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AiAppVersionCheckResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/builder-config")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<AiAppBuilderConfig>>> GetBuilderConfig(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiAppService.GetBuilderConfigAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AiAppBuilderConfig>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/builder-config")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateBuilderConfig(
        long id,
        [FromBody] AiAppBuilderConfig request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _aiAppService.UpdateBuilderConfigAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/resource-copy-tasks")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SubmitResourceCopy(
        long id,
        [FromBody] AiAppResourceCopyRequest request,
        CancellationToken cancellationToken)
    {
        _resourceCopyValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var taskId = await _aiAppService.SubmitResourceCopyAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TaskId = taskId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/resource-copy-tasks/latest")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<AiAppResourceCopyTaskProgress>>> GetLatestResourceCopyTask(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiAppService.GetLatestResourceCopyProgressAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiAppResourceCopyTaskProgress>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "AiResourceCopyTaskNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiAppResourceCopyTaskProgress>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/preview-run")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<AiAppPreviewRunResult>>> PreviewRun(
        long id,
        [FromBody] AiAppPreviewRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;

        var appDetail = await _aiAppService.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AI 应用不存在。", ErrorCodes.NotFound);
        var builderConfig = await _aiAppService.GetBuilderConfigAsync(tenantId, id, cancellationToken);

        var workflowId = ResolveWorkflowId(builderConfig.BoundWorkflowId, appDetail.WorkflowId);
        if (!workflowId.HasValue)
        {
            throw new BusinessException("应用未绑定可运行的工作流。", ErrorCodes.ValidationError);
        }

        var inputs = request.Inputs ?? new Dictionary<string, object?>();
        var execution = await _workflowExecutionService.SyncRunAsync(
            tenantId,
            workflowId.Value,
            userId,
            new CozeWorkflowRunCommand(JsonSerializer.Serialize(inputs), "draft"),
            cancellationToken);

        AiAppPreviewTrace? trace = null;
        if (long.TryParse(execution.ExecutionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var executionId))
        {
            var runTrace = await _workflowQueryService.GetRunTraceAsync(tenantId, executionId, cancellationToken);
            if (runTrace is not null)
            {
                trace = new AiAppPreviewTrace(
                    runTrace.ExecutionId,
                    runTrace.Status.ToString(),
                    runTrace.StartedAt,
                    runTrace.CompletedAt,
                    runTrace.DurationMs,
                    (runTrace.Steps ?? [])
                    .Select(step => new AiAppPreviewTraceStep(
                        step.NodeKey,
                        step.Status.ToString(),
                        step.NodeType.ToString(),
                        step.DurationMs,
                        step.ErrorMessage))
                    .ToArray());
            }
        }

        var result = new AiAppPreviewRunResult(ParseOutputs(execution.OutputsJson), trace);
        return Ok(ApiResponse<AiAppPreviewRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    private static long? ResolveWorkflowId(string? boundWorkflowId, long? fallbackWorkflowId)
    {
        if (!string.IsNullOrWhiteSpace(boundWorkflowId)
            && long.TryParse(boundWorkflowId.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var boundId)
            && boundId > 0)
        {
            return boundId;
        }

        return fallbackWorkflowId is > 0 ? fallbackWorkflowId : null;
    }

    private static IReadOnlyDictionary<string, object?> ParseOutputs(string? outputsJson)
    {
        if (string.IsNullOrWhiteSpace(outputsJson))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(outputsJson);
            return parsed ?? new Dictionary<string, object?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>
            {
                ["raw"] = outputsJson
            };
        }
    }
}
