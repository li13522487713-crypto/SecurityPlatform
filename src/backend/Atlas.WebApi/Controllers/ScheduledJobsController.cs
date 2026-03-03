using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 定时任务管理（Hangfire Recurring Jobs）
/// </summary>
[ApiController]
[Route("api/v1/scheduled-jobs")]
[Authorize]
public sealed class ScheduledJobsController : ControllerBase
{
    private readonly IScheduledJobService _jobService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly ITenantProvider _tenantProvider;

    public ScheduledJobsController(
        IScheduledJobService jobService,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        ITenantProvider tenantProvider)
    {
        _jobService = jobService;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.JobView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ScheduledJobDto>>>> Get(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobService.GetPagedAsync(pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<ScheduledJobDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{jobId}/trigger")]
    [Authorize(Policy = PermissionPolicies.JobTrigger)]
    public async Task<ActionResult<ApiResponse<object>>> Trigger(
        string jobId, CancellationToken cancellationToken = default)
    {
        await _jobService.TriggerAsync(jobId, cancellationToken);
        await RecordAuditAsync("TRIGGER_JOB", jobId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { jobId }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{jobId}/enable")]
    [Authorize(Policy = PermissionPolicies.JobUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Enable(
        string jobId, CancellationToken cancellationToken = default)
    {
        await _jobService.SetEnabledAsync(jobId, true, cancellationToken);
        await RecordAuditAsync("ENABLE_JOB", jobId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { jobId }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{jobId}/disable")]
    [Authorize(Policy = PermissionPolicies.JobUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Disable(
        string jobId, CancellationToken cancellationToken = default)
    {
        await _jobService.SetEnabledAsync(jobId, false, cancellationToken);
        await RecordAuditAsync("DISABLE_JOB", jobId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { jobId }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{jobId}/executions")]
    [Authorize(Policy = PermissionPolicies.JobView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ScheduledJobExecutionDto>>>> GetExecutions(
        string jobId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobService.GetExecutionsPagedAsync(jobId, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<ScheduledJobExecutionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    private async Task RecordAuditAsync(string action, string target, CancellationToken ct)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return;

        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;

        var auditContext = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, ct);
    }
}
