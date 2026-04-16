using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Application.AiPlatform.Models;

public sealed record WorkflowTraceSpanDto(
    string TraceId,
    string SpanId,
    string Name,
    ExecutionStatus Status,
    long DurationMs,
    string? InputJson,
    string? OutputJson);

public sealed record WorkflowTraceSnapshotDto(
    string ExecutionId,
    ExecutionStatus Status,
    IReadOnlyList<WorkflowTraceSpanDto> Spans);

public sealed record WorkflowCollaboratorDto(
    string UserId,
    string DisplayName,
    string RoleCode,
    bool Enabled);

public sealed record WorkflowTriggerDto(
    string Id,
    string WorkflowId,
    string Name,
    string EventType,
    bool Enabled);

public sealed record WorkflowJobDto(
    string Id,
    string WorkflowId,
    string Name,
    string Status,
    DateTime CreatedAtUtc);

public sealed record WorkflowTaskDto(
    string Id,
    string JobId,
    string Status,
    string? ErrorMessage);

public sealed record ChatFlowRoleDto(
    string Id,
    string WorkflowId,
    string Name,
    string Description,
    string? AvatarUri);
