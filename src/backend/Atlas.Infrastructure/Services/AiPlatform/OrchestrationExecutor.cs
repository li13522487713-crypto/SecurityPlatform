using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
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

    public OrchestrationExecutor(
        IOrchestrationCompiler compiler,
        IAtlasHybridCache cache)
    {
        _compiler = compiler;
        _cache = cache;
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
        var attempt = 0;
        var output = request.InputJson;
        string? errorMessage = null;

        foreach (var node in compiledPlan.Nodes)
        {
            var nodeSucceeded = false;
            var nodeAttempt = 0;
            while (!nodeSucceeded && nodeAttempt <= maxRetries)
            {
                nodeAttempt++;
                attempt++;
                var stepStartedAt = DateTimeOffset.UtcNow;
                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                    output = await ExecuteNodeAsync(node, output, timeoutCts.Token);
                    var completedAt = DateTimeOffset.UtcNow;
                    traceSteps.Add(new OrchestrationExecutionTraceStep(
                        node.NodeId,
                        node.NodeType,
                        "Success",
                        nodeAttempt,
                        (int)(completedAt - stepStartedAt).TotalMilliseconds,
                        null,
                        stepStartedAt,
                        completedAt));
                    nodeSucceeded = true;
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    var completedAt = DateTimeOffset.UtcNow;
                    errorMessage = $"节点执行超时: {node.NodeId}";
                    traceSteps.Add(new OrchestrationExecutionTraceStep(
                        node.NodeId,
                        node.NodeType,
                        "Timeout",
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
                    traceSteps.Add(new OrchestrationExecutionTraceStep(
                        node.NodeId,
                        node.NodeType,
                        "Failed",
                        nodeAttempt,
                        (int)(completedAt - stepStartedAt).TotalMilliseconds,
                        ex.Message,
                        stepStartedAt,
                        completedAt));
                }
            }

            if (!nodeSucceeded)
            {
                var failedResult = new OrchestrationExecutionResult(
                    request.PlanId,
                    executionId,
                    "Failed",
                    output,
                    attempt,
                    false,
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
}
