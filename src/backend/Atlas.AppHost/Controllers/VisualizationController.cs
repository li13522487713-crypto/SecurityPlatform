using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Visualization.Abstractions;
using Atlas.Application.Visualization.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Domain.Approval.Enums;
using Atlas.Presentation.Shared.Helpers;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 流程可视化中心控制器（骨架版）
/// </summary>
[ApiController]
[Route("api/v1/visualization")]
public sealed class VisualizationController : ControllerBase
{
    private readonly IVisualizationQueryService _queryService;
    private readonly IAuditRecorder _auditRecorder;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;

    public VisualizationController(
        IVisualizationQueryService queryService,
        IAuditRecorder auditRecorder,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor)
    {
        _queryService = queryService;
        _auditRecorder = auditRecorder;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
    }

    [HttpGet("overview")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<VisualizationOverviewResponse>>> GetOverview(
        [FromQuery] string? department,
        [FromQuery] string? flowType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var filter = new VisualizationFilterRequest
        {
            Department = department,
            FlowType = flowType,
            From = from,
            To = to
        };

        var overview = await _queryService.GetOverviewAsync(filter, cancellationToken);
        return Ok(ApiResponse<VisualizationOverviewResponse>.Ok(overview, HttpContext.TraceIdentifier));
    }

    [HttpGet("processes")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<VisualizationProcessSummary>>>> GetProcesses(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetProcessesAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<VisualizationProcessSummary>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("processes/{id}")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<VisualizationProcessDetail>>> GetProcess(
        string id,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetProcessAsync(id, cancellationToken);
        if (detail == null)
        {
            return NotFound(ApiResponse<VisualizationProcessDetail>.Fail("NOT_FOUND",
                string.Format(ApiResponseLocalizer.T(HttpContext, "VisualizationProcessNotFoundFormat"), id),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<VisualizationProcessDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("instances")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<VisualizationInstanceSummary>>>> GetInstances(
        [FromQuery] PagedRequest request,
        [FromQuery] long? processId,
        [FromQuery] ApprovalInstanceStatus? status,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetInstancesAsync(request, processId, status, cancellationToken);
        return Ok(ApiResponse<PagedResult<VisualizationInstanceSummary>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("instances/{id}")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<VisualizationInstanceDetail>>> GetInstance(
        string id,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetInstanceAsync(id, cancellationToken);
        if (detail == null)
        {
            return NotFound(ApiResponse<VisualizationInstanceDetail>.Fail("NOT_FOUND",
                string.Format(ApiResponseLocalizer.T(HttpContext, "VisualizationInstanceNotFoundFormat"), id),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<VisualizationInstanceDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost("processes/validation")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<VisualizationValidationResponse>>> ValidateProcess(
        [FromBody] ValidateVisualizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.ValidateAsync(request, cancellationToken);
        return Ok(ApiResponse<VisualizationValidationResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("processes")]
    [Authorize(Policy = PermissionPolicies.VisualizationProcessSave)]
    public async Task<ActionResult<ApiResponse<SaveVisualizationProcessResponse>>> SaveProcess(
        [FromBody] SaveVisualizationProcessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.SaveProcessAsync(request, cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            ApiResponseLocalizer.T(HttpContext, "AuditActionVisualizationSave"),
            ApiResponseLocalizer.T(HttpContext, "AuditOutcomeSuccess"),
            ApiResponseLocalizer.T(HttpContext, "AuditDetailVisualizationProcessIdFormat", result.ProcessId),
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return Ok(ApiResponse<SaveVisualizationProcessResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("processes/{id}")]
    [Authorize(Policy = PermissionPolicies.VisualizationProcessUpdate)]
    public async Task<ActionResult<ApiResponse<SaveVisualizationProcessResponse>>> UpdateProcess(
        string id,
        [FromBody] SaveVisualizationProcessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.SaveProcessAsync(request with { ProcessId = id }, cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            ApiResponseLocalizer.T(HttpContext, "AuditActionVisualizationUpdate"),
            ApiResponseLocalizer.T(HttpContext, "AuditOutcomeSuccess"),
            ApiResponseLocalizer.T(HttpContext, "AuditDetailVisualizationProcessIdFormat", result.ProcessId),
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return Ok(ApiResponse<SaveVisualizationProcessResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("processes/{id}/publication")]
    [Authorize(Policy = PermissionPolicies.VisualizationProcessPublish)]
    public async Task<ActionResult<ApiResponse<VisualizationPublishResponse>>> PublishProcess(
        string id,
        [FromBody] PublishVisualizationRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var payload = request with { ProcessId = id };
        var result = await _queryService.PublishAsync(payload, currentUser.UserId, cancellationToken);

        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            ApiResponseLocalizer.T(HttpContext, "AuditActionVisualizationPublish"),
            ApiResponseLocalizer.T(HttpContext, "AuditOutcomeSuccess"),
            ApiResponseLocalizer.T(HttpContext, "AuditDetailVisualizationProcessIdFormat", payload.ProcessId),
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return Ok(ApiResponse<VisualizationPublishResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("metrics")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<VisualizationMetricsResponse>>> GetMetrics(
        [FromQuery] string? department,
        [FromQuery] string? flowType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var filter = new VisualizationFilterRequest
        {
            Department = department,
            FlowType = flowType,
            From = from,
            To = to
        };

        var result = await _queryService.GetMetricsAsync(filter, cancellationToken);
        return Ok(ApiResponse<VisualizationMetricsResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("audit")]
    [Authorize(Policy = PermissionPolicies.VisualizationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditListItem>>>> GetAudit(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetAuditAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<AuditListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
