using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/workflow-playground")]
public sealed class WorkflowWorkbenchController : ControllerBase
{
    private readonly ICozeWorkflowExecutionService _executionService;
    private readonly ICozeWorkflowQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<WorkflowWorkbenchExecuteRequest> _validator;

    public WorkflowWorkbenchController(
        ICozeWorkflowExecutionService executionService,
        ICozeWorkflowQueryService queryService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<WorkflowWorkbenchExecuteRequest> validator)
    {
        _executionService = executionService;
        _queryService = queryService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _validator = validator;
    }

    [HttpPost("{id:long}/execute")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<WorkflowWorkbenchExecuteResultDto>>> Execute(
        long id,
        [FromBody] WorkflowWorkbenchExecuteRequest request,
        CancellationToken cancellationToken)
    {
        _validator.ValidateAndThrow(request);

        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var normalizedSource = string.IsNullOrWhiteSpace(request.Source)
            ? "draft"
            : request.Source.Trim().ToLowerInvariant();
        var inputsJson = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["incident"] = request.Incident.Trim()
        });

        var execution = await _executionService.SyncRunAsync(
            tenantId,
            id,
            userId,
            new CozeWorkflowRunCommand(inputsJson, normalizedSource),
            cancellationToken);

        WorkflowWorkbenchTraceDto? traceDto = null;
        if (long.TryParse(execution.ExecutionId, NumberStyles.None, CultureInfo.InvariantCulture, out var executionId))
        {
            var trace = await _queryService.GetRunTraceAsync(tenantId, executionId, cancellationToken);
            traceDto = trace is null ? null : MapTrace(trace);
        }

        var result = new WorkflowWorkbenchExecuteResultDto(
            new WorkflowWorkbenchExecutionDto(
                execution.ExecutionId,
                execution.Status?.ToString(),
                execution.OutputsJson,
                execution.ErrorMessage),
            traceDto);

        return Ok(ApiResponse<WorkflowWorkbenchExecuteResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    private static WorkflowWorkbenchTraceDto MapTrace(DagWorkflowRunTraceDto trace)
    {
        return new WorkflowWorkbenchTraceDto(
            trace.ExecutionId,
            trace.Status.ToString(),
            trace.StartedAt,
            trace.CompletedAt,
            trace.DurationMs,
            (trace.Steps ?? [])
                .Select(step => new WorkflowWorkbenchTraceStepDto(
                    step.NodeKey,
                    step.Status.ToString(),
                    step.NodeType.ToString(),
                    step.DurationMs,
                    step.ErrorMessage))
                .ToList());
    }
}
