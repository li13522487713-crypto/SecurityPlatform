using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Application.AiPlatform.Models;

public sealed record MultiAgentMemberInput(
    long AgentId,
    string? Alias,
    int SortOrder,
    bool IsEnabled,
    string? PromptPrefix);

public sealed record MultiAgentMemberItem(
    long AgentId,
    string? Alias,
    int SortOrder,
    bool IsEnabled,
    string? PromptPrefix);

public sealed record MultiAgentOrchestrationCreateRequest(
    string Name,
    string? Description,
    MultiAgentOrchestrationMode Mode,
    IReadOnlyList<MultiAgentMemberInput> Members);

public sealed record MultiAgentOrchestrationUpdateRequest(
    string Name,
    string? Description,
    MultiAgentOrchestrationMode Mode,
    IReadOnlyList<MultiAgentMemberInput> Members,
    MultiAgentOrchestrationStatus? Status);

public sealed record MultiAgentOrchestrationListItem(
    long Id,
    string Name,
    string? Description,
    MultiAgentOrchestrationMode Mode,
    MultiAgentOrchestrationStatus Status,
    int MemberCount,
    long CreatorUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record MultiAgentOrchestrationDetail(
    long Id,
    string Name,
    string? Description,
    MultiAgentOrchestrationMode Mode,
    MultiAgentOrchestrationStatus Status,
    long CreatorUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<MultiAgentMemberItem> Members);

public sealed record MultiAgentRunRequest(
    string Message,
    bool? EnableRag);

public sealed record MultiAgentExecutionStep(
    long AgentId,
    string AgentName,
    string? Alias,
    string InputMessage,
    string? OutputMessage,
    ExecutionStatus Status,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record MultiAgentExecutionResult(
    long ExecutionId,
    long OrchestrationId,
    ExecutionStatus Status,
    string? OutputMessage,
    string? ErrorMessage,
    IReadOnlyList<MultiAgentExecutionStep> Steps,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record MultiAgentStreamEvent(
    string EventType,
    string Data);
