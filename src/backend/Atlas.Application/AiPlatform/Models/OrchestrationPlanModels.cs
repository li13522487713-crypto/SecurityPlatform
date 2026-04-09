namespace Atlas.Application.AiPlatform.Models;

public sealed record CompiledOrchestrationNode(
    string NodeId,
    string NodeType,
    IReadOnlyList<string> DependsOn);

public sealed record CompiledOrchestrationPlan(
    long PlanId,
    string PlanKey,
    string PlanName,
    string TriggerType,
    int PublishedVersion,
    IReadOnlyList<CompiledOrchestrationNode> Nodes,
    string RuntimePolicyJson,
    string SourceNodeGraphJson,
    string PlanHash,
    DateTimeOffset CompiledAt);

public sealed record OrchestrationExecutionRequest(
    long PlanId,
    string? IdempotencyKey,
    string InputJson,
    int? MaxRetries = null,
    int? TimeoutSeconds = null);

public sealed record OrchestrationExecutionTraceStep(
    string NodeId,
    string NodeType,
    string Status,
    int Attempt,
    int DurationMs,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record OrchestrationExecutionResult(
    long PlanId,
    string ExecutionId,
    string Status,
    string OutputJson,
    int AttemptCount,
    bool IdempotentReplay,
    IReadOnlyList<OrchestrationExecutionTraceStep> TraceSteps,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
