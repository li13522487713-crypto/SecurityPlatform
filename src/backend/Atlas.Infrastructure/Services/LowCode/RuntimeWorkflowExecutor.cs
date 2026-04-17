using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 运行时工作流执行服务实现（M09 S09-1 / S09-2 / S09-4）。
///
/// 桥接 IDagWorkflowExecutionService（Coze DAG 引擎），并增强：
///  - 弹性策略：超时 / 重试 / 熔断 / 降级（按工作流粒度隔离）
///  - 异步任务：基于 SQLite 持久化（M09 简化版，M19 接入 Hangfire）
///  - 批量执行：同步循环 invoke + 失败策略（M09 单事务批量；大批量在 M19 走 Hangfire 分片）
///
/// 注：所有 workflowId 接受字符串（Coze 上游约定），内部转 long 调用 IDagWorkflowExecutionService。
/// </summary>
public sealed class RuntimeWorkflowExecutor : IRuntimeWorkflowExecutor
{
    private const int DefaultTimeoutMs = 30_000;
    private const int DefaultMaxAttempts = 3;
    private const int DefaultInitialDelayMs = 500;

    private static readonly Dictionary<string, CircuitState> Circuits = new();
    private static readonly object CircuitsSync = new();

    private readonly IDagWorkflowExecutionService _engine;
    private readonly IRuntimeWorkflowAsyncJobRepository _jobRepo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<RuntimeWorkflowExecutor> _logger;

    public RuntimeWorkflowExecutor(
        IDagWorkflowExecutionService engine,
        IRuntimeWorkflowAsyncJobRepository jobRepo,
        IIdGeneratorAccessor idGen,
        IAuditWriter auditWriter,
        ILogger<RuntimeWorkflowExecutor> logger)
    {
        _engine = engine;
        _jobRepo = jobRepo;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<RuntimeWorkflowInvokeResult> InvokeAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowInvokeRequest request, CancellationToken cancellationToken)
    {
        var policy = request.Resilience;
        var timeoutMs = policy?.TimeoutMs ?? DefaultTimeoutMs;
        var maxAttempts = policy?.Retry?.MaxAttempts ?? DefaultMaxAttempts;
        var initialDelay = policy?.Retry?.InitialDelayMs ?? DefaultInitialDelayMs;
        var backoff = policy?.Retry?.Backoff ?? "exponential";
        var circuitKey = $"workflow:{request.WorkflowId}";

        if (!CircuitAllow(circuitKey, policy?.CircuitBreaker))
        {
            return await TryFallbackAsync(tenantId, currentUserId, request, "circuit_open", cancellationToken)
                ?? throw new BusinessException("WORKFLOW_CIRCUIT_OPEN", $"工作流 {request.WorkflowId} 熔断中，且无降级策略");
        }

        Exception? lastError = null;
        for (var attempt = 1; attempt <= Math.Max(1, maxAttempts); attempt++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            try
            {
                var result = await DoInvokeAsync(tenantId, currentUserId, request, cts.Token);
                CircuitOnSuccess(circuitKey);
                await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.invoke", "success", $"wf:{request.WorkflowId}:exec:{result.ExecutionId}", null, null), cancellationToken);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested == false)
            {
                lastError = ex;
                CircuitOnFailure(circuitKey, policy?.CircuitBreaker);
                _logger.LogWarning(ex, "RuntimeWorkflow invoke attempt {Attempt}/{Max} failed: {Wf}", attempt, maxAttempts, request.WorkflowId);
                if (attempt < maxAttempts)
                {
                    var delay = backoff == "exponential" ? initialDelay * (int)Math.Pow(2, attempt - 1) : initialDelay;
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // 重试用尽 → 尝试 fallback；否则抛错
        var fallback = await TryFallbackAsync(tenantId, currentUserId, request, lastError?.Message ?? "exhausted", cancellationToken);
        if (fallback is not null) return fallback;

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.invoke", "failed", $"wf:{request.WorkflowId}:err:{lastError?.Message}", null, null), cancellationToken);
        throw new BusinessException("WORKFLOW_INVOKE_FAILED", lastError?.Message ?? "工作流调用失败");
    }

    public async Task<string> SubmitAsyncAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowInvokeRequest request, CancellationToken cancellationToken)
    {
        var jobId = $"awj_{_idGen.NextId()}";
        var requestJson = JsonSerializer.Serialize(request);
        var entity = new RuntimeWorkflowAsyncJob(tenantId, _idGen.NextId(), jobId, request.WorkflowId, requestJson, currentUserId);
        await _jobRepo.InsertAsync(entity, cancellationToken);

        // M09 简化：fire-and-forget 后台执行；M19 接入 Hangfire 持久化任务调度。
        _ = Task.Run(async () =>
        {
            try
            {
                entity.MarkRunning();
                await _jobRepo.UpdateAsync(entity, CancellationToken.None);
                var result = await InvokeAsync(tenantId, currentUserId, request, CancellationToken.None);
                entity.MarkSucceeded(JsonSerializer.Serialize(result));
                await _jobRepo.UpdateAsync(entity, CancellationToken.None);
            }
            catch (Exception ex)
            {
                entity.MarkFailed(ex.Message);
                await _jobRepo.UpdateAsync(entity, CancellationToken.None);
            }
        }, CancellationToken.None);

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.async.submit", "success", $"wf:{request.WorkflowId}:job:{jobId}", null, null), cancellationToken);
        return jobId;
    }

    public async Task<RuntimeWorkflowAsyncJobDto?> GetAsyncJobAsync(TenantId tenantId, string jobId, CancellationToken cancellationToken)
    {
        var job = await _jobRepo.FindByJobIdAsync(tenantId, jobId, cancellationToken);
        if (job is null) return null;
        RuntimeWorkflowInvokeResult? result = null;
        if (!string.IsNullOrWhiteSpace(job.ResultJson))
        {
            try { result = JsonSerializer.Deserialize<RuntimeWorkflowInvokeResult>(job.ResultJson); }
            catch { /* 容错：旧任务 result 可能格式不一致 */ }
        }
        return new RuntimeWorkflowAsyncJobDto(job.JobId, job.Status, job.SubmittedAt, job.CompletedAt, job.ProgressPercent, result);
    }

    public async Task CancelAsyncJobAsync(TenantId tenantId, long currentUserId, string jobId, CancellationToken cancellationToken)
    {
        var job = await _jobRepo.FindByJobIdAsync(tenantId, jobId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"异步任务不存在：{jobId}");
        if (job.Status is "success" or "failed" or "cancelled") return;
        job.MarkCancelled();
        await _jobRepo.UpdateAsync(job, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.async.cancel", "success", $"job:{jobId}", null, null), cancellationToken);
    }

    public async Task<RuntimeWorkflowBatchResult> InvokeBatchAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowBatchInvokeRequest request, CancellationToken cancellationToken)
    {
        var jobId = $"bwj_{_idGen.NextId()}";
        var rows = new List<RuntimeWorkflowBatchRowResult>(request.Rows.Count);
        var succeeded = 0;
        var failed = 0;
        var abort = string.Equals(request.OnFailure, "abort", StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < request.Rows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var single = await InvokeAsync(tenantId, currentUserId, new RuntimeWorkflowInvokeRequest(request.WorkflowId, request.Rows[i], request.AppId, request.PageId, null, null, null), cancellationToken);
                rows.Add(new RuntimeWorkflowBatchRowResult(i, "success", single.Outputs, null));
                succeeded++;
            }
            catch (Exception ex)
            {
                rows.Add(new RuntimeWorkflowBatchRowResult(i, "failed", null, ex.Message));
                failed++;
                if (abort) break;
            }
        }

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.workflow.batch", "success", $"wf:{request.WorkflowId}:job:{jobId}:total:{request.Rows.Count}:ok:{succeeded}:fail:{failed}", null, null), cancellationToken);
        return new RuntimeWorkflowBatchResult(jobId, request.Rows.Count, succeeded + failed, succeeded, failed, rows);
    }

    private async Task<RuntimeWorkflowInvokeResult> DoInvokeAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowInvokeRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.WorkflowId, out var workflowIdLong))
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"workflowId 必须为长整型字符串（DAG 引擎约定）：{request.WorkflowId}");
        }
        var inputsJson = request.Inputs is null ? null : JsonSerializer.Serialize(request.Inputs);
        var stopwatch = Stopwatch.StartNew();
        var run = await _engine.SyncRunAsync(tenantId, workflowIdLong, currentUserId, new DagWorkflowRunRequest(inputsJson, source: "lowcode-runtime"), cancellationToken);
        stopwatch.Stop();

        Dictionary<string, JsonElement>? outputs = null;
        if (!string.IsNullOrWhiteSpace(run.OutputsJson))
        {
            try { outputs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(run.OutputsJson); }
            catch { outputs = null; }
        }

        return new RuntimeWorkflowInvokeResult(
            ExecutionId: run.ExecutionId,
            Status: (run.Status?.ToString() ?? "success").ToLowerInvariant(),
            Outputs: outputs,
            Patches: null, // dispatch 控制器（M13）按 outputMapping 计算 patches
            ErrorMessage: run.ErrorMessage,
            TraceId: Activity.Current?.TraceId.ToString());
    }

    private async Task<RuntimeWorkflowInvokeResult?> TryFallbackAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowInvokeRequest request, string reason, CancellationToken cancellationToken)
    {
        var fallback = request.Resilience?.Fallback;
        if (fallback is null) return null;
        if (string.Equals(fallback.Kind, "static", StringComparison.OrdinalIgnoreCase) && fallback.StaticValue is JsonElement staticVal)
        {
            return new RuntimeWorkflowInvokeResult(
                ExecutionId: $"fallback-static-{Guid.NewGuid():N}",
                Status: "success",
                Outputs: new Dictionary<string, JsonElement> { ["value"] = staticVal },
                Patches: null,
                ErrorMessage: $"fallback(static) due to {reason}",
                TraceId: Activity.Current?.TraceId.ToString());
        }
        if (string.Equals(fallback.Kind, "workflow", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(fallback.WorkflowId))
        {
            // 调用降级 workflow（不再嵌套 fallback，避免环）
            var fbRequest = new RuntimeWorkflowInvokeRequest(fallback.WorkflowId!, request.Inputs, request.AppId, request.PageId, request.VersionId, request.ComponentId, null);
            try
            {
                return await DoInvokeAsync(tenantId, currentUserId, fbRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "fallback workflow {Wf} also failed", fallback.WorkflowId);
                return null;
            }
        }
        return null;
    }

    // ── 简易熔断 ──
    private sealed class CircuitState
    {
        public int Failures;
        public DateTimeOffset? OpenedAt;
    }

    private static bool CircuitAllow(string key, RuntimeCircuitBreakerPolicyDto? policy)
    {
        if (policy is null) return true;
        lock (CircuitsSync)
        {
            if (!Circuits.TryGetValue(key, out var s)) return true;
            if (s.OpenedAt is null) return true;
            var elapsed = DateTimeOffset.UtcNow - s.OpenedAt.Value;
            if (elapsed.TotalMilliseconds >= policy.OpenMs)
            {
                // 半开：允许一次试探
                s.Failures = 0;
                s.OpenedAt = null;
                return true;
            }
            return false;
        }
    }

    private static void CircuitOnSuccess(string key)
    {
        lock (CircuitsSync)
        {
            if (Circuits.TryGetValue(key, out var s))
            {
                s.Failures = 0;
                s.OpenedAt = null;
            }
        }
    }

    private static void CircuitOnFailure(string key, RuntimeCircuitBreakerPolicyDto? policy)
    {
        if (policy is null) return;
        lock (CircuitsSync)
        {
            if (!Circuits.TryGetValue(key, out var s))
            {
                s = new CircuitState();
                Circuits[key] = s;
            }
            s.Failures++;
            if (s.Failures >= policy.FailuresThreshold) s.OpenedAt = DateTimeOffset.UtcNow;
        }
    }
}
