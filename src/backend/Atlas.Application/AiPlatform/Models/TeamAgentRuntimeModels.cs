using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record TeamAgentRuntimeDescriptor(
    string RuntimeKey,
    string RuntimeDisplayName,
    string FrameworkFamily,
    string PackageId,
    string PackageVersion);

public sealed record TeamAgentMemberContribution(
    long AgentId,
    string AgentName,
    string RoleName,
    string? Alias,
    string InputMessage,
    string OutputMessage,
    int Round,
    DateTime StartedAt,
    DateTime CompletedAt);

public sealed record TeamAgentOrchestrationRuntimeRequest(
    TenantId TenantId,
    long UserId,
    TeamAgent TeamAgent,
    IReadOnlyList<TeamAgentMemberItem> Members,
    string Message,
    bool? EnableRag,
    string? AppId);

public sealed record TeamAgentOrchestrationRuntimeResult(
    string FinalMessage,
    IReadOnlyList<TeamAgentExecutionStep> Steps,
    IReadOnlyList<TeamAgentMemberContribution> Contributions,
    TeamAgentRuntimeDescriptor Runtime);
