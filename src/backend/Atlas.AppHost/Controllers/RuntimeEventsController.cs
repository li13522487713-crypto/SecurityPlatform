using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// dispatch 控制器（M13 S13-1，**runtime 前缀** /api/runtime/events/dispatch）。
///
/// 强约束（PLAN.md §1.3 #2）：UI 所有运行时事件必须经此入口。
/// </summary>
[ApiController]
[Route("api/runtime/events")]
[Authorize]
public sealed class RuntimeEventsController : ControllerBase
{
    private readonly IDispatchExecutor _executor;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeEventsController(IDispatchExecutor executor, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _executor = executor;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("dispatch")]
    public async Task<ActionResult<ApiResponse<DispatchResponse>>> Dispatch([FromBody] DispatchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _executor.DispatchAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<DispatchResponse>.Ok(r, HttpContext.TraceIdentifier));
    }
}

[ApiController]
[Route("api/runtime/traces")]
[Authorize]
public sealed class RuntimeTracesController : ControllerBase
{
    private readonly IRuntimeTraceService _trace;
    private readonly ITenantProvider _tenantProvider;

    public RuntimeTracesController(IRuntimeTraceService trace, ITenantProvider tenantProvider)
    {
        _trace = trace;
        _tenantProvider = tenantProvider;
    }

    /// <summary>查询 trace（6 维：traceId / page / component / 时间范围 / errorType / userId / 租户）。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RuntimeTraceDto>>>> Query(
        [FromQuery] string? traceId,
        [FromQuery] string? appId,
        [FromQuery] string? page,
        [FromQuery] string? component,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? errorType,
        [FromQuery] string? userId,
        [FromQuery] int? pageIndex,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var query = new RuntimeTraceQuery(traceId, appId, page, component, from, to, errorType, userId, pageIndex, pageSize);
        var list = await _trace.QueryAsync(tenantId, query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RuntimeTraceDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("{traceId}")]
    public async Task<ActionResult<ApiResponse<RuntimeTraceDto?>>> GetById(string traceId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var t = await _trace.GetTraceAsync(tenantId, traceId, cancellationToken);
        return Ok(ApiResponse<RuntimeTraceDto?>.Ok(t, HttpContext.TraceIdentifier));
    }
}
