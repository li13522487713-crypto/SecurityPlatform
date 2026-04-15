using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiAppListItem(
    long Id,
    string Name,
    string? Description,
    string? Icon,
    long? AgentId,
    long? WorkflowId,
    long? PromptTemplateId,
    AiAppStatus Status,
    int PublishVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt);

public sealed record AiAppDetail(
    long Id,
    string Name,
    string? Description,
    string? Icon,
    long? AgentId,
    long? WorkflowId,
    long? PromptTemplateId,
    AiAppStatus Status,
    int PublishVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<AiAppPublishRecordItem> PublishRecords);

public sealed record AiAppCreateRequest(
    string Name,
    string? Description,
    string? Icon,
    long? AgentId,
    long? WorkflowId,
    long? PromptTemplateId,
    long? WorkspaceId = null);

public sealed record AiAppUpdateRequest(
    string Name,
    string? Description,
    string? Icon,
    long? AgentId,
    long? WorkflowId,
    long? PromptTemplateId,
    long? WorkspaceId = null);

public sealed record AiAppPublishRequest(string? ReleaseNote);

public sealed record AiAppPublishRecordItem(
    long Id,
    long AppId,
    string Version,
    string? ReleaseNote,
    long PublishedByUserId,
    DateTime CreatedAt);

public sealed record AiAppVersionCheckResult(
    long AppId,
    int CurrentPublishVersion,
    string? LatestVersion,
    DateTime? LatestPublishedAt);

public sealed record AiAppResourceCopyRequest(long SourceAppId);

public sealed record AiAppResourceCopyTaskProgress(
    long TaskId,
    long AppId,
    long SourceAppId,
    AiAppResourceCopyStatus Status,
    int TotalItems,
    int CopiedItems,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiAppBuilderConfigOption(
    string Label,
    string Value);

public sealed record AiAppBuilderInputComponent(
    string Id,
    string Type,
    string Label,
    string VariableKey,
    bool Required,
    string? DefaultValue,
    IReadOnlyList<AiAppBuilderConfigOption>? Options);

public sealed record AiAppBuilderOutputComponent(
    string Id,
    string Type,
    string Label,
    string SourceExpression);

public sealed record AiAppBuilderConfig(
    IReadOnlyList<AiAppBuilderInputComponent> Inputs,
    IReadOnlyList<AiAppBuilderOutputComponent> Outputs,
    string? BoundWorkflowId,
    string LayoutMode);

public sealed record AiAppPreviewRunRequest(
    IReadOnlyDictionary<string, object?> Inputs);

public sealed record AiAppPreviewTraceStep(
    string NodeKey,
    string? Status,
    string? NodeType,
    long? DurationMs,
    string? ErrorMessage);

public sealed record AiAppPreviewTrace(
    string ExecutionId,
    string? Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? DurationMs,
    IReadOnlyList<AiAppPreviewTraceStep> Steps);

public sealed record AiAppPreviewRunResult(
    IReadOnlyDictionary<string, object?> Outputs,
    AiAppPreviewTrace? Trace);
