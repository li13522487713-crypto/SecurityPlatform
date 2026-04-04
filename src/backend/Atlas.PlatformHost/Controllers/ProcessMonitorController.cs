using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/process-monitor")]
public sealed class ProcessMonitorController : ControllerBase
{
    private readonly IProcessMonitorService _monitorService;
    private readonly ITenantProvider _tenantProvider;

    public ProcessMonitorController(
        IProcessMonitorService monitorService,
        ITenantProvider tenantProvider)
    {
        _monitorService = monitorService;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// 获取流程监控仪表盘数据
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<ProcessMonitorDashboard>>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dashboard = await _monitorService.GetDashboardAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<ProcessMonitorDashboard>.Ok(dashboard, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 获取流程实例追踪（含节点高亮）
    /// </summary>
    [HttpGet("instances/{instanceId:long}/trace")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<ProcessInstanceTrace?>>> GetInstanceTrace(
        long instanceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var trace = await _monitorService.GetInstanceTraceAsync(tenantId, instanceId, cancellationToken);
        return Ok(ApiResponse<ProcessInstanceTrace?>.Ok(trace, HttpContext.TraceIdentifier));
    }
}
