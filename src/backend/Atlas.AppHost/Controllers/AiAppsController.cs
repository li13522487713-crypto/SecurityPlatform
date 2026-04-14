using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/ai-apps")]
[Authorize]
public sealed class AiAppsController : ControllerBase
{
    private readonly IAiAppService _aiAppService;
    private readonly IWorkflowV2ExecutionService _workflowExecutionService;
    private readonly IWorkflowV2QueryService _workflowQueryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AiAppsController(
        IAiAppService aiAppService,
        IWorkflowV2ExecutionService workflowExecutionService,
        IWorkflowV2QueryService workflowQueryService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _aiAppService = aiAppService;
        _workflowExecutionService = workflowExecutionService;
        _workflowQueryService = workflowQueryService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
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
            new WorkflowV2RunRequest(JsonSerializer.Serialize(inputs), "draft"),
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
