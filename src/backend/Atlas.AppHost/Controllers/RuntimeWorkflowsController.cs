using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 运行时工作流控制器（M09 S09-1，**运行时 runtime 前缀** /api/runtime/workflows）。
///
/// - POST /{id}:invoke         同步执行
/// - POST /{id}:invoke-async   异步提交
/// - POST /{id}:invoke-batch   批量执行
///
/// 强约束（PLAN.md §1.3 #1）：仅作为 dispatch（M13）内部使用；UI 直调由 lowcode-runtime-web 走 dispatch 路由。
/// </summary>
[ApiController]
[Route("api/runtime/workflows")]
[Authorize]
public sealed class RuntimeWorkflowsController : ControllerBase
{
    private readonly IRuntimeWorkflowExecutor _executor;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeWorkflowsController(IRuntimeWorkflowExecutor executor, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _executor = executor;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("{id}:invoke")]
    public async Task<ActionResult<ApiResponse<RuntimeWorkflowInvokeResult>>> Invoke(string id, [FromBody] RuntimeWorkflowInvokeRequest request, CancellationToken cancellationToken)
    {
        EnsureWorkflowIdMatch(id, request.WorkflowId);
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _executor.InvokeAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<RuntimeWorkflowInvokeResult>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}:invoke-async")]
    public async Task<ActionResult<ApiResponse<object>>> InvokeAsync(string id, [FromBody] RuntimeWorkflowInvokeRequest request, CancellationToken cancellationToken)
    {
        EnsureWorkflowIdMatch(id, request.WorkflowId);
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var jobId = await _executor.SubmitAsyncAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { jobId }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}:invoke-batch")]
    public async Task<ActionResult<ApiResponse<RuntimeWorkflowBatchResult>>> InvokeBatch(string id, [FromBody] RuntimeWorkflowBatchInvokeRequest request, CancellationToken cancellationToken)
    {
        EnsureWorkflowIdMatch(id, request.WorkflowId);
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _executor.InvokeBatchAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<RuntimeWorkflowBatchResult>.Ok(r, HttpContext.TraceIdentifier));
    }

    private static void EnsureWorkflowIdMatch(string routeId, string bodyId)
    {
        if (!string.Equals(routeId, bodyId, StringComparison.Ordinal))
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"路由 workflowId({routeId}) 与 body.workflowId({bodyId}) 不一致");
        }
    }
}

/// <summary>
/// 运行时异步任务控制器（M09 S09-2）。
/// </summary>
[ApiController]
[Route("api/runtime/async-jobs")]
[Authorize]
public sealed class RuntimeAsyncJobsController : ControllerBase
{
    private readonly IRuntimeWorkflowExecutor _executor;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeAsyncJobsController(IRuntimeWorkflowExecutor executor, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _executor = executor;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet("{jobId}")]
    public async Task<ActionResult<ApiResponse<RuntimeWorkflowAsyncJobDto>>> Get(string jobId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var job = await _executor.GetAsyncJobAsync(tenantId, jobId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"异步任务不存在：{jobId}");
        return Ok(ApiResponse<RuntimeWorkflowAsyncJobDto>.Ok(job, HttpContext.TraceIdentifier));
    }

    [HttpPost("{jobId}:cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(string jobId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _executor.CancelAsyncJobAsync(tenantId, user.UserId, jobId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
