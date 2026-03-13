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
    long? PromptTemplateId);

public sealed record AiAppUpdateRequest(
    string Name,
    string? Description,
    string? Icon,
    long? AgentId,
    long? WorkflowId,
    long? PromptTemplateId);

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
