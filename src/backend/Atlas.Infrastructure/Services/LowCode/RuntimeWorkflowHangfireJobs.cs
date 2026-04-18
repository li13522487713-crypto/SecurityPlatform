using System.Text.Json;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// M19 收尾：把 RuntimeWorkflowExecutor 的 fire-and-forget 异步任务 + WorkflowBatchService 的同步循环
/// 替换为 Hangfire 后台作业，并用 RuntimeWorkflowAsyncJob.UpdateProgress 实现进度回调。
///
/// 设计：
/// - 单一 Job 类同时承载异步 + 批量两种入口（按 jobId 前缀区分：awj_=异步、bwj_=批量）；
/// - DisableConcurrentExecution 按 jobId 隔离避免重复执行；
/// - AutomaticRetry 由调用方按 RuntimeWorkflowExecutor 内部弹性策略管理（避免双重重试）。
/// </summary>
public sealed class RuntimeWorkflowBackgroundJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RuntimeWorkflowBackgroundJob> _logger;

    public RuntimeWorkflowBackgroundJob(IServiceScopeFactory scopeFactory, ILogger<RuntimeWorkflowBackgroundJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>异步任务执行（接管 RuntimeWorkflowExecutor.SubmitAsyncAsync 内部 Task.Run）。</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunAsyncJobAsync(Guid tenantGuid, long currentUserId, string jobId, string requestJson)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IRuntimeWorkflowAsyncJobRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<IRuntimeWorkflowExecutor>();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var tenantId = new TenantId(tenantGuid);
        var entity = await jobRepo.FindByJobIdAsync(tenantId, jobId, CancellationToken.None);
        if (entity is null)
        {
            _logger.LogWarning("RunAsyncJobAsync: jobId 不存在 {JobId}", jobId);
            return;
        }
        try
        {
            entity.MarkRunning();
            entity.UpdateProgress(10);
            await jobRepo.UpdateAsync(entity, CancellationToken.None);

            var request = JsonSerializer.Deserialize<RuntimeWorkflowInvokeRequest>(requestJson)
                ?? throw new InvalidOperationException("requestJson 反序列化失败");
            var result = await executor.InvokeAsync(tenantId, currentUserId, request, CancellationToken.None);

            entity.UpdateProgress(95);
            entity.MarkSucceeded(JsonSerializer.Serialize(result));
            await jobRepo.UpdateAsync(entity, CancellationToken.None);
            await auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.async.complete", "success", $"job:{jobId}", null, null), CancellationToken.None);
        }
        catch (Exception ex)
        {
            entity.MarkFailed(ex.Message);
            await jobRepo.UpdateAsync(entity, CancellationToken.None);
            await auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.async.complete", "failed", $"job:{jobId}:err:{ex.Message}", null, null), CancellationToken.None);
            _logger.LogError(ex, "async workflow job failed: {JobId}", jobId);
        }
    }

    /// <summary>批量任务执行（接管 WorkflowBatchService.ExecuteBatchAsync 同步循环）。</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunBatchJobAsync(Guid tenantGuid, long currentUserId, string jobId, string batchRequestJson)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IRuntimeWorkflowAsyncJobRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<IRuntimeWorkflowExecutor>();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var tenantId = new TenantId(tenantGuid);
        var entity = await jobRepo.FindByJobIdAsync(tenantId, jobId, CancellationToken.None);
        if (entity is null) return;
        try
        {
            entity.MarkRunning();
            await jobRepo.UpdateAsync(entity, CancellationToken.None);

            var batchReq = JsonSerializer.Deserialize<RuntimeWorkflowBatchInvokeRequest>(batchRequestJson)
                ?? throw new InvalidOperationException("batchRequestJson 反序列化失败");
            var rows = batchReq.Rows;
            var total = rows.Count;
            var succeeded = 0;
            var failed = 0;
            var abort = string.Equals(batchReq.OnFailure, "abort", StringComparison.OrdinalIgnoreCase);
            for (var i = 0; i < total; i++)
            {
                try
                {
                    await executor.InvokeAsync(tenantId, currentUserId, new RuntimeWorkflowInvokeRequest(batchReq.WorkflowId, rows[i], batchReq.AppId, batchReq.PageId, null, null, null), CancellationToken.None);
                    succeeded++;
                }
                catch
                {
                    failed++;
                    if (abort) break;
                }
                if (i % 5 == 0)
                {
                    entity.UpdateProgress((int)((i + 1) * 100.0 / Math.Max(1, total)));
                    await jobRepo.UpdateAsync(entity, CancellationToken.None);
                }
            }
            var result = new RuntimeWorkflowBatchResult(jobId, total, succeeded + failed, succeeded, failed, null);
            entity.MarkSucceeded(JsonSerializer.Serialize(result));
            await jobRepo.UpdateAsync(entity, CancellationToken.None);
            await auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.batch.complete", "success", $"job:{jobId}:total:{total}:ok:{succeeded}:fail:{failed}", null, null), CancellationToken.None);
        }
        catch (Exception ex)
        {
            entity.MarkFailed(ex.Message);
            await jobRepo.UpdateAsync(entity, CancellationToken.None);
            _logger.LogError(ex, "batch workflow job failed: {JobId}", jobId);
        }
    }
}
