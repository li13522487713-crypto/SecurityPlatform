using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class Agent : TenantEntity
{
    public Agent()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        AvatarUrl = string.Empty;
        SystemPrompt = string.Empty;
        PersonaMarkdown = string.Empty;
        Goals = string.Empty;
        ReplyLogic = string.Empty;
        OutputFormat = string.Empty;
        Constraints = string.Empty;
        OpeningMessage = string.Empty;
        PresetQuestionsJson = "[]";
        DatabaseBindingsJson = "[]";
        VariableBindingsJson = "[]";
        LayoutConfigJson = "{}";
        DebugConfigJson = "{}";
        PublishedConnectorConfigJson = "{}";
        ModelName = string.Empty;
        Mode = AgentMode.Single;
        PromptVersion = string.Empty;
        UpdatedAt = DateTime.UnixEpoch;
        PublishedAt = DateTime.UnixEpoch;
        Status = AgentStatus.Draft;
        EnableMemory = true;
        EnableShortTermMemory = true;
        EnableLongTermMemory = true;
        LongTermMemoryTopK = 3;
    }

    public Agent(
        TenantId tenantId,
        string name,
        long creatorId,
        long id,
        long? workspaceId = null)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        WorkspaceId = workspaceId;
        CreatorId = creatorId;
        Status = AgentStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        PublishedAt = DateTime.UnixEpoch;
        EnableMemory = true;
        EnableShortTermMemory = true;
        EnableLongTermMemory = true;
        LongTermMemoryTopK = 3;
        PresetQuestionsJson = "[]";
        DatabaseBindingsJson = "[]";
        VariableBindingsJson = "[]";
        LayoutConfigJson = "{}";
        DebugConfigJson = "{}";
        PublishedConnectorConfigJson = "{}";
        PromptVersion = string.Empty;
        Mode = AgentMode.Single;
    }

    public string Name { get; private set; }
    public long? WorkspaceId { get; private set; }
    public string? Description { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? SystemPrompt { get; private set; }
    public string? PersonaMarkdown { get; private set; }
    public string? Goals { get; private set; }
    public string? ReplyLogic { get; private set; }
    public string? OutputFormat { get; private set; }
    public string? Constraints { get; private set; }
    public string? OpeningMessage { get; private set; }
    public string PresetQuestionsJson { get; private set; }
    public string DatabaseBindingsJson { get; private set; }
    public string VariableBindingsJson { get; private set; }
    public AgentMode Mode { get; private set; }
    public long? PromptTemplateId { get; private set; }
    public string PromptVersion { get; private set; }
    public string LayoutConfigJson { get; private set; }
    public string DebugConfigJson { get; private set; }
    public string PublishedConnectorConfigJson { get; private set; }
    public long? ModelConfigId { get; private set; }
    public string? ModelName { get; private set; }
    public float? Temperature { get; private set; }
    public int? MaxTokens { get; private set; }
    public long? DefaultWorkflowId { get; private set; }
    public string? DefaultWorkflowName { get; private set; }
    public AgentStatus Status { get; private set; }
    public long CreatorId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int PublishVersion { get; private set; }
    public bool EnableMemory { get; private set; }
    public bool EnableShortTermMemory { get; private set; }
    public bool EnableLongTermMemory { get; private set; }
    public int LongTermMemoryTopK { get; private set; }

    public void Update(
        string name,
        string? description,
        string? avatarUrl,
        string? systemPrompt,
        string? personaMarkdown,
        string? goals,
        string? replyLogic,
        string? outputFormat,
        string? constraints,
        string? openingMessage,
        string? presetQuestionsJson,
        string? databaseBindingsJson,
        string? variableBindingsJson,
        long? modelConfigId,
        string? modelName,
        float? temperature,
        int? maxTokens,
        long? defaultWorkflowId = null,
        string? defaultWorkflowName = null,
        bool? enableMemory = null,
        bool? enableShortTermMemory = null,
        bool? enableLongTermMemory = null,
        int? longTermMemoryTopK = null,
        AgentMode? mode = null,
        long? promptTemplateId = null,
        string? promptVersion = null,
        string? layoutConfigJson = null,
        string? debugConfigJson = null,
        string? publishedConnectorConfigJson = null,
        long? workspaceId = null)
    {
        Name = name;
        if (workspaceId.HasValue)
        {
            WorkspaceId = workspaceId.Value;
        }
        Description = description ?? string.Empty;
        AvatarUrl = avatarUrl ?? string.Empty;
        SystemPrompt = systemPrompt ?? string.Empty;
        PersonaMarkdown = personaMarkdown ?? string.Empty;
        Goals = goals ?? string.Empty;
        ReplyLogic = replyLogic ?? string.Empty;
        OutputFormat = outputFormat ?? string.Empty;
        Constraints = constraints ?? string.Empty;
        OpeningMessage = openingMessage ?? string.Empty;
        PresetQuestionsJson = string.IsNullOrWhiteSpace(presetQuestionsJson) ? "[]" : presetQuestionsJson;
        DatabaseBindingsJson = string.IsNullOrWhiteSpace(databaseBindingsJson) ? "[]" : databaseBindingsJson;
        VariableBindingsJson = string.IsNullOrWhiteSpace(variableBindingsJson) ? "[]" : variableBindingsJson;
        Mode = mode ?? Mode;
        PromptTemplateId = promptTemplateId;
        PromptVersion = promptVersion ?? string.Empty;
        LayoutConfigJson = string.IsNullOrWhiteSpace(layoutConfigJson) ? "{}" : layoutConfigJson;
        DebugConfigJson = string.IsNullOrWhiteSpace(debugConfigJson) ? "{}" : debugConfigJson;
        PublishedConnectorConfigJson = string.IsNullOrWhiteSpace(publishedConnectorConfigJson) ? "{}" : publishedConnectorConfigJson;
        ModelConfigId = modelConfigId;
        if (!ModelConfigId.HasValue)
        {
            ModelConfigId = 0;
        }
        ModelName = modelName ?? string.Empty;
        Temperature = temperature;
        if (!Temperature.HasValue)
        {
            Temperature = 0;
        }

        MaxTokens = maxTokens;
        if (!MaxTokens.HasValue)
        {
            MaxTokens = 0;
        }

        DefaultWorkflowId = defaultWorkflowId;
        if (!DefaultWorkflowId.HasValue)
        {
            DefaultWorkflowId = 0;
        }

        DefaultWorkflowName = defaultWorkflowName ?? string.Empty;

        if (enableMemory.HasValue)
        {
            EnableMemory = enableMemory.Value;
        }

        if (enableShortTermMemory.HasValue)
        {
            EnableShortTermMemory = enableShortTermMemory.Value;
        }

        if (enableLongTermMemory.HasValue)
        {
            EnableLongTermMemory = enableLongTermMemory.Value;
        }

        if (longTermMemoryTopK.HasValue && longTermMemoryTopK.Value > 0)
        {
            LongTermMemoryTopK = longTermMemoryTopK.Value;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = AgentStatus.Published;
        PublishVersion++;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        Status = AgentStatus.Disabled;
        UpdatedAt = DateTime.UtcNow;
        if (!PublishedAt.HasValue)
        {
            PublishedAt = DateTime.UnixEpoch;
        }
    }

    public Agent CreateDuplicate(long newId, string newName, long creatorId)
    {
        var duplicate = new Agent(TenantId, newName, creatorId, newId, WorkspaceId);
        duplicate.Update(
            newName,
            Description,
            AvatarUrl,
            SystemPrompt,
            PersonaMarkdown,
            Goals,
            ReplyLogic,
            OutputFormat,
            Constraints,
            OpeningMessage,
            PresetQuestionsJson,
            DatabaseBindingsJson,
            VariableBindingsJson,
            ModelConfigId,
            ModelName,
            Temperature,
            MaxTokens,
            DefaultWorkflowId,
            DefaultWorkflowName,
            EnableMemory,
            EnableShortTermMemory,
            EnableLongTermMemory,
            LongTermMemoryTopK);
        return duplicate;
    }

    public void AssignWorkspace(long workspaceId)
    {
        if (workspaceId <= 0)
        {
            return;
        }

        WorkspaceId = workspaceId;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AgentStatus
{
    Draft = 0,
    Published = 1,
    Disabled = 2
}

public enum AgentMode
{
    Single = 0,
    Workflow = 1
}
