namespace Atlas.Application.AiPlatform.Models;

public sealed record AiAppConversationTemplateListItem(
    long Id,
    long AppId,
    string Name,
    string CreateMethod,
    long? SourceWorkflowId,
    string? SourceWorkflowName,
    long? ConnectorId,
    bool IsDefault,
    int Version,
    int PublishedVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiAppConversationTemplateDetail(
    long Id,
    long AppId,
    string Name,
    string CreateMethod,
    long? SourceWorkflowId,
    string? SourceWorkflowName,
    long? ConnectorId,
    bool IsDefault,
    int Version,
    int PublishedVersion,
    string ConfigJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiAppConversationTemplateCreateRequest(
    string Name,
    string CreateMethod,
    long? SourceWorkflowId = null,
    long? ConnectorId = null,
    bool IsDefault = false,
    string? ConfigJson = null);

public sealed record AiAppConversationTemplateUpdateRequest(
    string Name,
    long? SourceWorkflowId = null,
    long? ConnectorId = null,
    bool? IsDefault = null,
    string? ConfigJson = null);
