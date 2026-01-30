using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Identity;
using Atlas.Application.Visualization.Abstractions;
using Atlas.Application.Visualization.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 流程可视化中心控制器（骨架版）
/// </summary>
[ApiController]
[Route("api/visualization")]
[Authorize]
public sealed class VisualizationController : ControllerBase
{
    private readonly IVisualizationQueryService _queryService;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public VisualizationController(
        IVisualizationQueryService queryService,
        IAuditWriter auditWriter,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _auditWriter = auditWriter;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("overview")]
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
    public async Task<ActionResult<ApiResponse<PagedResult<VisualizationProcessSummary>>>> GetProcesses(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetProcessesAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<VisualizationProcessSummary>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("processes/{id}")]
    public async Task<ActionResult<ApiResponse<VisualizationProcessDetail>>> GetProcess(
        string id,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetProcessAsync(id, cancellationToken);
        if (detail == null)
        {
            return NotFound(ApiResponse<VisualizationProcessDetail>.Fail("NOT_FOUND", $"流程不存在: {id}", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<VisualizationProcessDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("instances")]
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
    public async Task<ActionResult<ApiResponse<VisualizationInstanceDetail>>> GetInstance(
        string id,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetInstanceAsync(id, cancellationToken);
        if (detail == null)
        {
            return NotFound(ApiResponse<VisualizationInstanceDetail>.Fail("NOT_FOUND", $"实例不存在: {id}", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<VisualizationInstanceDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost("processes/validate")]
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
        var clientContext = ControllerHelper.GetClientContext(HttpContext);
        var auditRecord = new AuditRecord(
            tenantId: currentUser.TenantId,
            actor: currentUser.UserId.ToString(),
            action: "可视化流程-保存",
            result: "成功",
            target: $"流程ID: {result.ProcessId}",
            ipAddress: ControllerHelper.GetIpAddress(HttpContext),
            userAgent: ControllerHelper.GetUserAgent(HttpContext),
            clientType: clientContext.ClientType.ToString(),
            clientPlatform: clientContext.ClientPlatform.ToString(),
            clientChannel: clientContext.ClientChannel.ToString(),
            clientAgent: clientContext.ClientAgent.ToString());
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

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
        var clientContext = ControllerHelper.GetClientContext(HttpContext);
        var auditRecord = new AuditRecord(
            tenantId: currentUser.TenantId,
            actor: currentUser.UserId.ToString(),
            action: "可视化流程-更新",
            result: "成功",
            target: $"流程ID: {result.ProcessId}",
            ipAddress: ControllerHelper.GetIpAddress(HttpContext),
            userAgent: ControllerHelper.GetUserAgent(HttpContext),
            clientType: clientContext.ClientType.ToString(),
            clientPlatform: clientContext.ClientPlatform.ToString(),
            clientChannel: clientContext.ClientChannel.ToString(),
            clientAgent: clientContext.ClientAgent.ToString());
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

        return Ok(ApiResponse<SaveVisualizationProcessResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("processes/publish")]
    [Authorize(Policy = PermissionPolicies.VisualizationProcessPublish)]
    public async Task<ActionResult<ApiResponse<VisualizationPublishResponse>>> PublishProcess(
        [FromBody] PublishVisualizationRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _queryService.PublishAsync(request, currentUser.UserId, cancellationToken);

        var clientContext = ControllerHelper.GetClientContext(HttpContext);
        var auditRecord = new AuditRecord(
            tenantId: currentUser.TenantId,
            actor: currentUser.UserId.ToString(),
            action: "可视化流程-发布",
            result: "成功",
            target: $"流程ID: {request.ProcessId}",
            ipAddress: ControllerHelper.GetIpAddress(HttpContext),
            userAgent: ControllerHelper.GetUserAgent(HttpContext),
            clientType: clientContext.ClientType.ToString(),
            clientPlatform: clientContext.ClientPlatform.ToString(),
            clientChannel: clientContext.ClientChannel.ToString(),
            clientAgent: clientContext.ClientAgent.ToString());
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

        return Ok(ApiResponse<VisualizationPublishResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("metrics")]
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
    public async Task<ActionResult<ApiResponse<PagedResult<AuditListItem>>>> GetAudit(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetAuditAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<AuditListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
