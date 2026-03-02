namespace Atlas.Application.LowCode.Models;

public sealed record MessageTemplateListItem(string Id, string Name, string Channel, string? EventType, string? Description, bool IsActive, DateTimeOffset CreatedAt);
public sealed record MessageTemplateDetail(string Id, string Name, string Channel, string? EventType, string ContentTemplate, string? SubjectTemplate, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record MessageTemplateCreateRequest(string Name, string Channel, string? EventType, string ContentTemplate, string? SubjectTemplate, string? Description);
public sealed record MessageTemplateUpdateRequest(string Name, string Channel, string? EventType, string ContentTemplate, string? SubjectTemplate, string? Description);

public sealed record MessageRecordListItem(string Id, string Channel, string? RecipientAddress, string? Subject, string Status, int RetryCount, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);
public sealed record MessageRecordDetail(string Id, long? TemplateId, string Channel, string? RecipientId, string? RecipientAddress, string? Subject, string Content, string? EventType, string Status, int RetryCount, string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset? SentAt, DateTimeOffset? ReadAt);

public sealed record SendMessageRequest(string Channel, string? TemplateId, string? RecipientId, string? RecipientAddress, string? Subject, string? Content, string? EventType, System.Collections.Generic.Dictionary<string, string>? Variables);

public sealed record ChannelConfigItem(string Id, string Channel, string ConfigJson, bool IsActive, DateTimeOffset UpdatedAt);
public sealed record ChannelConfigUpdateRequest(string ConfigJson, bool IsActive);
