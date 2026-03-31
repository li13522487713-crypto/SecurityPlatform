using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using System.Text.Json.Serialization;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class TeamAgent : TenantEntity
{
    public TeamAgent()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        TeamMode = TeamAgentMode.GroupChat;
        Status = TeamAgentStatus.Draft;
        CapabilityTagsJson = "[]";
        DefaultEntrySkill = string.Empty;
        BoundDataAssetsJson = "[]";
        MembersJson = "[]";
        SchemaConfigJson = "{}";
        LegacySourceType = string.Empty;
        LegacySourceId = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public TeamAgent(
        TenantId tenantId,
        string name,
        string? description,
        TeamAgentMode teamMode,
        string capabilityTagsJson,
        string? defaultEntrySkill,
        string boundDataAssetsJson,
        string membersJson,
        string schemaConfigJson,
        long creatorUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        TeamMode = teamMode;
        Status = TeamAgentStatus.Draft;
        CapabilityTagsJson = string.IsNullOrWhiteSpace(capabilityTagsJson) ? "[]" : capabilityTagsJson;
        DefaultEntrySkill = defaultEntrySkill ?? string.Empty;
        BoundDataAssetsJson = string.IsNullOrWhiteSpace(boundDataAssetsJson) ? "[]" : boundDataAssetsJson;
        MembersJson = string.IsNullOrWhiteSpace(membersJson) ? "[]" : membersJson;
        SchemaConfigJson = string.IsNullOrWhiteSpace(schemaConfigJson) ? "{}" : schemaConfigJson;
        LegacySourceType = string.Empty;
        LegacySourceId = string.Empty;
        CreatorUserId = creatorUserId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public TeamAgentMode TeamMode { get; private set; }
    public TeamAgentStatus Status { get; private set; }
    public string CapabilityTagsJson { get; private set; }
    public string? DefaultEntrySkill { get; private set; }
    public string BoundDataAssetsJson { get; private set; }
    public string MembersJson { get; private set; }
    public string SchemaConfigJson { get; private set; }
    public long CreatorUserId { get; private set; }
    public int PublishVersion { get; private set; }
    public string? LegacySourceType { get; private set; }
    public string? LegacySourceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        TeamAgentMode teamMode,
        TeamAgentStatus? status,
        string capabilityTagsJson,
        string? defaultEntrySkill,
        string boundDataAssetsJson,
        string membersJson,
        string schemaConfigJson)
    {
        Name = name;
        Description = description ?? string.Empty;
        TeamMode = teamMode;
        CapabilityTagsJson = string.IsNullOrWhiteSpace(capabilityTagsJson) ? "[]" : capabilityTagsJson;
        DefaultEntrySkill = defaultEntrySkill ?? string.Empty;
        BoundDataAssetsJson = string.IsNullOrWhiteSpace(boundDataAssetsJson) ? "[]" : boundDataAssetsJson;
        MembersJson = string.IsNullOrWhiteSpace(membersJson) ? "[]" : membersJson;
        SchemaConfigJson = string.IsNullOrWhiteSpace(schemaConfigJson) ? "{}" : schemaConfigJson;
        if (status.HasValue)
        {
            Status = status.Value;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = TeamAgentStatus.Active;
        PublishVersion++;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = PublishedAt.Value;
    }

    public void SetLegacySource(string? legacySourceType, string? legacySourceId)
    {
        LegacySourceType = legacySourceType ?? string.Empty;
        LegacySourceId = legacySourceId ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class TeamAgentPublication : TenantEntity
{
    public TeamAgentPublication()
        : base(TenantId.Empty)
    {
        SnapshotJson = "{}";
        ReleaseNote = string.Empty;
        PublishedAt = DateTime.UtcNow;
        IsActive = true;
        RevokedAt = DateTime.UnixEpoch;
    }

    public TeamAgentPublication(
        TenantId tenantId,
        long teamAgentId,
        int version,
        string snapshotJson,
        string? releaseNote,
        long publishedByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        TeamAgentId = teamAgentId;
        Version = version;
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? "{}" : snapshotJson;
        ReleaseNote = releaseNote ?? string.Empty;
        PublishedByUserId = publishedByUserId;
        PublishedAt = DateTime.UtcNow;
        IsActive = true;
        RevokedAt = DateTime.UnixEpoch;
    }

    public long TeamAgentId { get; private set; }
    public int Version { get; private set; }
    public string SnapshotJson { get; private set; }
    public string? ReleaseNote { get; private set; }
    public long PublishedByUserId { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RevokedAt = DateTime.UtcNow;
    }
}

public sealed class TeamAgentTemplate : TenantEntity
{
    public TeamAgentTemplate()
        : base(TenantId.Empty)
    {
        Key = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        CapabilityTagsJson = "[]";
        DefaultEntrySkill = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public TeamAgentTemplate(
        TenantId tenantId,
        string key,
        string name,
        string description,
        TeamAgentMode teamMode,
        string capabilityTagsJson,
        string? defaultEntrySkill,
        bool isSystem,
        long id)
        : base(tenantId)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        TeamMode = teamMode;
        CapabilityTagsJson = string.IsNullOrWhiteSpace(capabilityTagsJson) ? "[]" : capabilityTagsJson;
        DefaultEntrySkill = defaultEntrySkill ?? string.Empty;
        IsSystem = isSystem;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Key { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public TeamAgentMode TeamMode { get; private set; }
    public string CapabilityTagsJson { get; private set; }
    public string? DefaultEntrySkill { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

public sealed class TeamAgentTemplateMember : TenantEntity
{
    public TeamAgentTemplateMember()
        : base(TenantId.Empty)
    {
        RoleName = string.Empty;
        Responsibility = string.Empty;
        Alias = string.Empty;
        PromptPrefix = string.Empty;
        CapabilityTagsJson = "[]";
        CreatedAt = DateTime.UtcNow;
    }

    public TeamAgentTemplateMember(
        TenantId tenantId,
        long templateId,
        string roleName,
        string? responsibility,
        string? alias,
        int sortOrder,
        bool isEnabled,
        string? promptPrefix,
        string capabilityTagsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        TemplateId = templateId;
        RoleName = roleName;
        Responsibility = responsibility ?? string.Empty;
        Alias = alias ?? string.Empty;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        PromptPrefix = promptPrefix ?? string.Empty;
        CapabilityTagsJson = string.IsNullOrWhiteSpace(capabilityTagsJson) ? "[]" : capabilityTagsJson;
        CreatedAt = DateTime.UtcNow;
    }

    public long TemplateId { get; private set; }
    public string RoleName { get; private set; }
    public string? Responsibility { get; private set; }
    public string? Alias { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? PromptPrefix { get; private set; }
    public string CapabilityTagsJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public sealed class TeamAgentConversation : TenantEntity
{
    public TeamAgentConversation()
        : base(TenantId.Empty)
    {
        Title = string.Empty;
        CreatedAt = DateTime.UtcNow;
        LastMessageAt = CreatedAt;
    }

    public TeamAgentConversation(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        string? title,
        long id)
        : base(tenantId)
    {
        Id = id;
        TeamAgentId = teamAgentId;
        UserId = userId;
        Title = title;
        CreatedAt = DateTime.UtcNow;
        LastMessageAt = CreatedAt;
        LastContextClearedAt = DateTime.UnixEpoch;
    }

    public long TeamAgentId { get; private set; }
    public long UserId { get; private set; }
    public string? Title { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }
    public DateTime LastContextClearedAt { get; private set; }

    public void AddMessage(DateTime messageAt)
    {
        MessageCount++;
        LastMessageAt = messageAt;
    }

    public void RemoveMessage(DateTime? latestMessageAt)
    {
        MessageCount = Math.Max(0, MessageCount - 1);
        LastMessageAt = latestMessageAt ?? DateTime.UnixEpoch;
    }

    public void ResetMessages()
    {
        MessageCount = 0;
        LastMessageAt = DateTime.UnixEpoch;
    }

    public void UpdateTitle(string title)
    {
        Title = title;
    }

    public void ClearContext(DateTime clearedAt)
    {
        LastContextClearedAt = clearedAt;
    }
}

public sealed class TeamAgentMessage : TenantEntity
{
    public TeamAgentMessage()
        : base(TenantId.Empty)
    {
        Role = string.Empty;
        Content = string.Empty;
        Metadata = string.Empty;
        EventType = string.Empty;
        MemberName = string.Empty;
    }

    public TeamAgentMessage(
        TenantId tenantId,
        long conversationId,
        string role,
        string content,
        string? metadata,
        string eventType,
        string? memberName,
        bool isContextCleared,
        long id)
        : base(tenantId)
    {
        Id = id;
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Metadata = metadata ?? string.Empty;
        EventType = eventType;
        MemberName = memberName ?? string.Empty;
        IsContextCleared = isContextCleared;
        CreatedAt = DateTime.UtcNow;
    }

    public long ConversationId { get; private set; }
    public string Role { get; private set; }
    public string Content { get; private set; }
    public string? Metadata { get; private set; }
    public string EventType { get; private set; }
    public string? MemberName { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsContextCleared { get; private set; }
}

public sealed class TeamAgentExecution : TenantEntity
{
    public TeamAgentExecution()
        : base(TenantId.Empty)
    {
        InputMessage = string.Empty;
        OutputMessage = string.Empty;
        ErrorMessage = string.Empty;
        TraceJson = "[]";
        EventTraceJson = "[]";
        Status = TeamAgentExecutionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        StartedAt = CreatedAt;
    }

    public TeamAgentExecution(
        TenantId tenantId,
        long teamAgentId,
        long conversationId,
        long userId,
        string inputMessage,
        long id)
        : base(tenantId)
    {
        Id = id;
        TeamAgentId = teamAgentId;
        ConversationId = conversationId;
        UserId = userId;
        InputMessage = inputMessage;
        OutputMessage = string.Empty;
        ErrorMessage = string.Empty;
        TraceJson = "[]";
        EventTraceJson = "[]";
        Status = TeamAgentExecutionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        StartedAt = CreatedAt;
    }

    public long TeamAgentId { get; private set; }
    public long ConversationId { get; private set; }
    public long UserId { get; private set; }
    public string InputMessage { get; private set; }
    public string? OutputMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string TraceJson { get; private set; }
    public string EventTraceJson { get; private set; }
    public TeamAgentExecutionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public void MarkRunning()
    {
        Status = TeamAgentExecutionStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(string? outputMessage, string traceJson, string eventTraceJson)
    {
        Status = TeamAgentExecutionStatus.Completed;
        OutputMessage = outputMessage ?? string.Empty;
        ErrorMessage = string.Empty;
        TraceJson = string.IsNullOrWhiteSpace(traceJson) ? "[]" : traceJson;
        EventTraceJson = string.IsNullOrWhiteSpace(eventTraceJson) ? "[]" : eventTraceJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string? errorMessage, string traceJson, string eventTraceJson)
    {
        Status = TeamAgentExecutionStatus.Failed;
        ErrorMessage = errorMessage ?? string.Empty;
        TraceJson = string.IsNullOrWhiteSpace(traceJson) ? "[]" : traceJson;
        EventTraceJson = string.IsNullOrWhiteSpace(eventTraceJson) ? "[]" : eventTraceJson;
        CompletedAt = DateTime.UtcNow;
    }
}

[SugarTable("TeamAgentExecutionStep")]
public sealed class TeamAgentExecutionStepEntity : TenantEntity
{
    public TeamAgentExecutionStepEntity()
        : base(TenantId.Empty)
    {
        AgentName = string.Empty;
        RoleName = string.Empty;
        Alias = string.Empty;
        InputMessage = string.Empty;
        OutputMessage = string.Empty;
        Status = string.Empty;
        ErrorMessage = string.Empty;
        StartedAt = DateTime.UtcNow;
    }

    public TeamAgentExecutionStepEntity(
        TenantId tenantId,
        long executionId,
        long? agentId,
        string agentName,
        string roleName,
        string? alias,
        string inputMessage,
        string? outputMessage,
        string status,
        string? errorMessage,
        DateTime startedAt,
        DateTime? completedAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        ExecutionId = executionId;
        AgentId = agentId;
        AgentName = agentName;
        RoleName = roleName;
        Alias = alias ?? string.Empty;
        InputMessage = inputMessage;
        OutputMessage = outputMessage ?? string.Empty;
        Status = status;
        ErrorMessage = errorMessage ?? string.Empty;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    public long ExecutionId { get; private set; }
    public long? AgentId { get; private set; }
    public string AgentName { get; private set; }
    public string RoleName { get; private set; }
    public string? Alias { get; private set; }
    public string InputMessage { get; private set; }
    public string? OutputMessage { get; private set; }
    public string Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
}

public sealed class TeamAgentSchemaDraft : TenantEntity
{
    public TeamAgentSchemaDraft()
        : base(TenantId.Empty)
    {
        Title = string.Empty;
        Requirement = string.Empty;
        DraftJson = "{}";
        OpenQuestionsJson = "[]";
        CreatedTableKeysJson = "[]";
        CreatedResourcesJson = "[]";
        ConfirmationState = TeamAgentSchemaDraftConfirmationState.Pending;
        Status = TeamAgentSchemaDraftStatus.Active;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public TeamAgentSchemaDraft(
        TenantId tenantId,
        long teamAgentId,
        long? conversationId,
        long creatorUserId,
        string title,
        string requirement,
        string draftJson,
        string openQuestionsJson,
        string appId,
        long id)
        : base(tenantId)
    {
        Id = id;
        TeamAgentId = teamAgentId;
        ConversationId = conversationId;
        CreatorUserId = creatorUserId;
        Title = title;
        Requirement = requirement;
        DraftJson = string.IsNullOrWhiteSpace(draftJson) ? "{}" : draftJson;
        OpenQuestionsJson = string.IsNullOrWhiteSpace(openQuestionsJson) ? "[]" : openQuestionsJson;
        CreatedTableKeysJson = "[]";
        CreatedResourcesJson = "[]";
        AppId = appId ?? string.Empty;
        ConfirmationState = TeamAgentSchemaDraftConfirmationState.Pending;
        Status = TeamAgentSchemaDraftStatus.Active;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long TeamAgentId { get; private set; }
    public long? ConversationId { get; private set; }
    public long CreatorUserId { get; private set; }
    public string Title { get; private set; }
    public string Requirement { get; private set; }
    public string DraftJson { get; private set; }
    public string OpenQuestionsJson { get; private set; }
    public string CreatedTableKeysJson { get; private set; }
    public string CreatedResourcesJson { get; private set; }
    public string? AppId { get; private set; }
    public TeamAgentSchemaDraftConfirmationState ConfirmationState { get; private set; }
    public TeamAgentSchemaDraftStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? DiscardedAt { get; private set; }

    public void UpdateDraft(string title, string requirement, string draftJson, string openQuestionsJson)
    {
        Title = title;
        Requirement = requirement;
        DraftJson = string.IsNullOrWhiteSpace(draftJson) ? "{}" : draftJson;
        OpenQuestionsJson = string.IsNullOrWhiteSpace(openQuestionsJson) ? "[]" : openQuestionsJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm(string createdTableKeysJson, string createdResourcesJson)
    {
        ConfirmationState = TeamAgentSchemaDraftConfirmationState.Confirmed;
        CreatedTableKeysJson = string.IsNullOrWhiteSpace(createdTableKeysJson) ? "[]" : createdTableKeysJson;
        CreatedResourcesJson = string.IsNullOrWhiteSpace(createdResourcesJson) ? "[]" : createdResourcesJson;
        ConfirmedAt = DateTime.UtcNow;
        UpdatedAt = ConfirmedAt.Value;
    }

    public void Discard()
    {
        Status = TeamAgentSchemaDraftStatus.Discarded;
        ConfirmationState = TeamAgentSchemaDraftConfirmationState.Discarded;
        DiscardedAt = DateTime.UtcNow;
        UpdatedAt = DiscardedAt.Value;
    }
}

[SugarTable("TeamAgentSchemaDraftExecutionAudit")]
public sealed class TeamAgentSchemaDraftExecutionAudit : TenantEntity
{
    public TeamAgentSchemaDraftExecutionAudit()
        : base(TenantId.Empty)
    {
        Stage = string.Empty;
        Action = string.Empty;
        Status = string.Empty;
        ResourceKey = string.Empty;
        ResourceId = string.Empty;
        Detail = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public TeamAgentSchemaDraftExecutionAudit(
        TenantId tenantId,
        long draftId,
        int sequence,
        string stage,
        string action,
        string status,
        string? resourceKey,
        string? resourceId,
        string? detail,
        long id)
        : base(tenantId)
    {
        Id = id;
        DraftId = draftId;
        Sequence = sequence;
        Stage = stage;
        Action = action;
        Status = status;
        ResourceKey = resourceKey ?? string.Empty;
        ResourceId = resourceId ?? string.Empty;
        Detail = detail ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public long DraftId { get; private set; }
    public int Sequence { get; private set; }
    public string Stage { get; private set; }
    public string Action { get; private set; }
    public string Status { get; private set; }
    public string? ResourceKey { get; private set; }
    public string? ResourceId { get; private set; }
    public string? Detail { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamAgentMode
{
    GroupChat = 0,
    Workflow = 1,
    Handoff = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamAgentStatus
{
    Draft = 0,
    Active = 1,
    Disabled = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamAgentExecutionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamAgentSchemaDraftConfirmationState
{
    Pending = 0,
    Confirmed = 1,
    Discarded = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamAgentSchemaDraftStatus
{
    Active = 0,
    Discarded = 1
}
