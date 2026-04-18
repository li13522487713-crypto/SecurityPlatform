using System.Text.Json;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// DAG 工作流异步执行（M19 S19-3，v2 前缀 /api/v2/workflows/{id}/async）。
/// 与 /api/runtime/workflows/{id}:invoke-async 不同：本控制器接受 webhook 回调，作业完成时由后台 job 自动 POST 回调。
/// </summary>
[ApiController]
[Route("api/v2/workflows")]
[Authorize]
public sealed class DagWorkflowAsyncController : ControllerBase
{
    private readonly IRuntimeWorkflowExecutor _executor;
    private readonly IRuntimeWorkflowAsyncJobRepository _jobRepo;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public DagWorkflowAsyncController(IRuntimeWorkflowExecutor executor, IRuntimeWorkflowAsyncJobRepository jobRepo, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _executor = executor;
        _jobRepo = jobRepo;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    public sealed record AsyncSubmitRequest(
        string WorkflowId,
        Dictionary<string, JsonElement>? Inputs,
        string? AppId,
        string? PageId,
        /// <summary>HTTPS Webhook URL；终态时 POST { jobId, status, result } 回调。</summary>
        string? WebhookUrl);

    [HttpPost("{id}/async")]
    public async Task<ActionResult<ApiResponse<object>>> Submit(string id, [FromBody] AsyncSubmitRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(id, request.WorkflowId, StringComparison.Ordinal))
            throw new BusinessException(ErrorCodes.ValidationError, $"路由 workflowId({id}) 与 body.workflowId({request.WorkflowId}) 不一致");
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();

        var jobId = await _executor.SubmitAsyncAsync(tenantId, user.UserId, new RuntimeWorkflowInvokeRequest(id, request.Inputs, request.AppId, request.PageId, null, null, null), cancellationToken);

        // 写入 webhookUrl（在 RuntimeWorkflowExecutor.SubmitAsyncAsync 已建好任务后再补字段）
        if (!string.IsNullOrWhiteSpace(request.WebhookUrl))
        {
            var entity = await _jobRepo.FindByJobIdAsync(tenantId, jobId, cancellationToken);
            if (entity is not null)
            {
                entity.SetWebhook(request.WebhookUrl);
                await _jobRepo.UpdateAsync(entity, cancellationToken);
            }
        }
        return Ok(ApiResponse<object>.Ok(new { jobId, webhookUrl = request.WebhookUrl }, HttpContext.TraceIdentifier));
    }

    [HttpGet("async-jobs/{jobId}")]
    public async Task<ActionResult<ApiResponse<RuntimeWorkflowAsyncJobDto>>> Get(string jobId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _executor.GetAsyncJobAsync(tenantId, jobId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"任务不存在：{jobId}");
        return Ok(ApiResponse<RuntimeWorkflowAsyncJobDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPost("async-jobs/{jobId}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(string jobId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _executor.CancelAsyncJobAsync(tenantId, user.UserId, jobId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Webhook 接收端点（demo / 自检用）。第三方系统可指向此端点验证回调连通性。
    /// 仅记录到日志，不参与业务处理。
    /// </summary>
    [HttpPost("async-jobs/webhook")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> WebhookEcho([FromBody] JsonElement payload)
    {
        return Ok(ApiResponse<object>.Ok(new { received = payload, at = DateTimeOffset.UtcNow }, HttpContext.TraceIdentifier));
    }
}
