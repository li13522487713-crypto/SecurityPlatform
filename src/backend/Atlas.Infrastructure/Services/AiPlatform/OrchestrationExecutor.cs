using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OrchestrationExecutor : IOrchestrationExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IOrchestrationCompiler _compiler;
    private readonly IAtlasHybridCache _cache;
    private readonly ICheckpointService _checkpointService;
    private readonly IOrchestrationCompensationService _compensationService;

    public OrchestrationExecutor(
        IOrchestrationCompiler compiler,
        IAtlasHybridCache cache,
        ICheckpointService checkpointService,
        IOrchestrationCompensationService compensationService)
    {
        _compiler = compiler;
        _cache = cache;
        _checkpointService = checkpointService;
        _compensationService = compensationService;
    }

    public async Task<OrchestrationExecutionResult> ExecuteAsync(
        TenantId tenantId,
        OrchestrationExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdempotencyKey = request.IdempotencyKey?.Trim();
        var maxRetries = request.MaxRetries.GetValueOrDefault(1);
        var timeoutSeconds = request.TimeoutSeconds.GetValueOrDefault(30);
        if (maxRetries < 0)
        {
            maxRetries = 0;
        }
        if (timeoutSeconds <= 0)
        {
            timeoutSeconds = 30;
        }

        var startedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey))
        {
            var cacheKey = BuildIdempotencyCacheKey(tenantId, request.PlanId, normalizedIdempotencyKey);
            var hit = await _cache.TryGetAsync<OrchestrationExecutionResult>(cacheKey, cancellationToken: cancellationToken);
            if (hit.Found && hit.Value is not null)
            {
                return hit.Value with { IdempotentReplay = true };
            }
        }

        var compiledPlan = await _compiler.CompileByIdAsync(tenantId, request.PlanId, cancellationToken);
        if (compiledPlan is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, $"编排计划不存在: {request.PlanId}");
        }

        var traceSteps = new List<OrchestrationExecutionTraceStep>(compiledPlan.Nodes.Count);
        var executionId = Guid.NewGuid().ToString("N");
        var shardExecutionId = BuildShardExecutionId(tenantId, request.PlanId, normalizedIdempotencyKey, executionId);
        var attempt = 0;
        var output = request.InputJson;
        string? errorMessage = null;
        var resumeApplied = false;
        var compensationApplied = false;
        var resumeFromStepIndex = 0;

        if (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey))
        {
            var checkpoint = await _checkpointService.GetLatestAsync(shardExecutionId, cancellationToken);
            if (checkpoint is not null)
            {
                resumeFromStepIndex = (int)Math.Clamp(checkpoint.ProcessedCount, 0, compiledPlan.Nodes.Count);
                resumeApplied = resumeFromStepIndex > 0;
            }
        }

        for (var nodeIndex = resumeFromStepIndex; nodeIndex < compiledPlan.Nodes.Count; nodeIndex++)
        {
            var node = compiledPlan.Nodes[nodeIndex];
            var nodeSucceeded = false;
            var nodeAttempt = 0;
            var lifecycle = NodeLifecycleState.Pending;
            while (!nodeSucceeded && nodeAttempt <= maxRetries)
            {
                nodeAttempt++;
                attempt++;
                var stepStartedAt = DateTimeOffset.UtcNow;
                try
                {
                    if (NodeLifecycleStateMachine.CanTransit(lifecycle, NodeLifecycleState.Running))
                    {
                        lifecycle = NodeLifecycleState.Running;
                    }
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                    output = await ExecuteNodeAsync(node, output, timeoutCts.Token);
                    var completedAt = DateTimeOffset.UtcNow;
                    if (NodeLifecycleStateMachine.CanTransit(lifecycle, NodeLifecycleState.Succeeded))
                    {
                        lifecycle = NodeLifecycleState.Succeeded;
                    }
                    traceSteps.Add(new OrchestrationExecutionTraceStep(
                        node.NodeId,
                        node.NodeType,
                        NodeLifecycleStateMachine.ToStatus(lifecycle),
                        nodeAttempt,
                        (int)(completedAt - stepStartedAt).TotalMilliseconds,
                        null,
                        stepStartedAt,
                        completedAt));
                    await _checkpointService.SaveAsync(
                        shardExecutionId,
                        executionId,
                        node.NodeId,
                        nodeIndex + 1,
                        tenantId,
                        cancellationToken);
                    nodeSucceeded = true;
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    var completedAt = DateTimeOffset.UtcNow;
                    errorMessage = $"节点执行超时: {node.NodeId}";
                    if (NodeLifecycleStateMachine.CanTransit(lifecycle, NodeLifecycleState.TimedOut))
                    {
                        lifecycle = NodeLifecycleState.TimedOut;
                    }
                    traceSteps.Add(new OrchestrationExecutionTraceStep(
                        node.NodeId,
                        node.NodeType,
                        NodeLifecycleStateMachine.ToStatus(lifecycle),
                        nodeAttempt,
                        (int)(completedAt - stepStartedAt).TotalMilliseconds,
                        ex.Message,
                        stepStartedAt,
                        completedAt));
                }
                catch (Exception ex)
                {
                    var completedAt = DateTimeOffset.UtcNow;
                    errorMessage = ex.Message;
                    if (NodeLifecycleStateMachine.CanTransit(lifecycle, NodeLifecycleState.Failed))
                    {
                        lifecycle = NodeLifecycleState.Failed;
                    }
                    traceSteps.Add(new OrchestrationExecutionTraceStep(
                        node.NodeId,
                        node.NodeType,
                        NodeLifecycleStateMachine.ToStatus(lifecycle),
                        nodeAttempt,
                        (int)(completedAt - stepStartedAt).TotalMilliseconds,
                        ex.Message,
                        stepStartedAt,
                        completedAt));
                }
            }

            if (!nodeSucceeded)
            {
                var compensatedSteps = await _compensationService.CompensateAsync(
                    tenantId,
                    request.PlanId,
                    executionId,
                    traceSteps,
                    cancellationToken);
                if (compensatedSteps.Count > 0)
                {
                    traceSteps.AddRange(compensatedSteps);
                    compensationApplied = true;
                }

                var failedResult = new OrchestrationExecutionResult(
                    request.PlanId,
                    executionId,
                    "Failed",
                    output,
                    attempt,
                    false,
                    resumeApplied,
                    compensationApplied,
                    traceSteps,
                    errorMessage ?? "执行失败",
                    startedAt,
                    DateTimeOffset.UtcNow);
                await CacheIdempotentResultAsync(tenantId, request.PlanId, normalizedIdempotencyKey, failedResult, cancellationToken);
                return failedResult;
            }
        }

        var successOutput = BuildSuccessOutputJson(output, compiledPlan, traceSteps);
        var result = new OrchestrationExecutionResult(
            request.PlanId,
            executionId,
            "Succeeded",
            successOutput,
            attempt,
            false,
            resumeApplied,
            compensationApplied,
            traceSteps,
            null,
            startedAt,
            DateTimeOffset.UtcNow);
        await CacheIdempotentResultAsync(tenantId, request.PlanId, normalizedIdempotencyKey, result, cancellationToken);
        return result;
    }

    private static Task<string> ExecuteNodeAsync(
        CompiledOrchestrationNode node,
        string input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (node.NodeType.Equals("fail", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"节点触发失败: {node.NodeId}");
        }

        var output = JsonSerializer.Serialize(new
        {
            nodeId = node.NodeId,
            nodeType = node.NodeType,
            previous = input
        });
        return Task.FromResult(output);
    }

    private static string BuildSuccessOutputJson(
        string output,
        CompiledOrchestrationPlan plan,
        IReadOnlyCollection<OrchestrationExecutionTraceStep> traceSteps)
    {
        return JsonSerializer.Serialize(new
        {
            planId = plan.PlanId,
            planKey = plan.PlanKey,
            planHash = plan.PlanHash,
            nodeCount = plan.Nodes.Count,
            stepCount = traceSteps.Count,
            output
        }, JsonOptions);
    }

    private async Task CacheIdempotentResultAsync(
        TenantId tenantId,
        long planId,
        string? idempotencyKey,
        OrchestrationExecutionResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return;
        }

        var cacheKey = BuildIdempotencyCacheKey(tenantId, planId, idempotencyKey);
        await _cache.SetAsync(
            cacheKey,
            result,
            TimeSpan.FromHours(24),
            tags: [$"orchestration-plan:{planId}"],
            cancellationToken: cancellationToken);
    }

    private static string BuildIdempotencyCacheKey(TenantId tenantId, long planId, string idempotencyKey)
        => $"orchestration:execute:{tenantId.Value:N}:{planId}:{idempotencyKey}";

    private static long BuildShardExecutionId(
        TenantId tenantId,
        long planId,
        string? idempotencyKey,
        string executionId)
    {
        var source = string.IsNullOrWhiteSpace(idempotencyKey)
            ? $"{tenantId.Value:N}:{planId}:{executionId}"
            : $"{tenantId.Value:N}:{planId}:{idempotencyKey}";
        var hash = BitConverter.ToInt64(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)), 0);
        return hash == long.MinValue ? long.MaxValue : Math.Abs(hash);
    }
}
