using System.Text.Json;

namespace Atlas.Application.LowCode.Models;

/// <summary>
/// RuntimeWorkflowsController DTO 集（M09 S09-1）。
///
/// 与前端 @atlas/lowcode-workflow-adapter/types 完全对齐。
/// </summary>
public sealed record RuntimeWorkflowInvokeRequest(
    string WorkflowId,
    Dictionary<string, JsonElement>? Inputs,
    string? AppId,
    string? PageId,
    string? VersionId,
    string? ComponentId,
    /// <summary>可覆盖默认弹性策略（每次调用单独配置）。</summary>
    RuntimeResiliencePolicyDto? Resilience);

public sealed record RuntimeWorkflowInvokeResult(
    string ExecutionId,
    string Status,
    Dictionary<string, JsonElement>? Outputs,
    /// <summary>状态补丁（M03 RuntimeStatePatch 镜像，前端 store.applyPatches 直接消费）。</summary>
    IReadOnlyList<RuntimeStatePatchDto>? Patches,
    string? ErrorMessage,
    string? TraceId);

public sealed record RuntimeStatePatchDto(
    string Scope,
    string Path,
    string Op,
    JsonElement? Value,
    string? ComponentId);

public sealed record RuntimeWorkflowAsyncJobDto(
    string JobId,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? CompletedAt,
    int? ProgressPercent,
    RuntimeWorkflowInvokeResult? Result);

public sealed record RuntimeWorkflowBatchInvokeRequest(
    string WorkflowId,
    IReadOnlyList<Dictionary<string, JsonElement>> Rows,
    /// <summary>失败策略：continue 继续 / abort 立即中止。</summary>
    string? OnFailure,
    string? AppId,
    string? PageId);

public sealed record RuntimeWorkflowBatchResult(
    string JobId,
    int Total,
    int Completed,
    int Succeeded,
    int Failed,
    IReadOnlyList<RuntimeWorkflowBatchRowResult>? Rows);

public sealed record RuntimeWorkflowBatchRowResult(
    int Index,
    string Status,
    Dictionary<string, JsonElement>? Outputs,
    string? ErrorMessage);

public sealed record RuntimeResiliencePolicyDto(
    int? TimeoutMs,
    RuntimeRetryPolicyDto? Retry,
    RuntimeCircuitBreakerPolicyDto? CircuitBreaker,
    RuntimeFallbackPolicyDto? Fallback);

public sealed record RuntimeRetryPolicyDto(int MaxAttempts, string Backoff, int? InitialDelayMs);

public sealed record RuntimeCircuitBreakerPolicyDto(int FailuresThreshold, int WindowMs, int OpenMs);

public sealed record RuntimeFallbackPolicyDto(string Kind, string? WorkflowId, JsonElement? StaticValue);
