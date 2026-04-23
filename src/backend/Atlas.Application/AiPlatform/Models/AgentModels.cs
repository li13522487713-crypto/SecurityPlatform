namespace Atlas.Application.AiPlatform.Models;

public sealed record AgentListItem(
    long Id,
    string Name,
    string? Description,
    string? AvatarUrl,
    string Status,
    string? ModelName,
    DateTime CreatedAt,
    int PublishVersion);

public sealed record AgentDetail(
    long Id,
    string Name,
    string? Description,
    string? AvatarUrl,
    string? SystemPrompt,
    string? PersonaMarkdown,
    string? Goals,
    string? ReplyLogic,
    string? OutputFormat,
    string? Constraints,
    string? OpeningMessage,
    IReadOnlyList<string>? PresetQuestions,
    IReadOnlyList<AgentKnowledgeBindingItem>? KnowledgeBindings,
    IReadOnlyList<AgentDatabaseBindingItem>? DatabaseBindings,
    IReadOnlyList<AgentVariableBindingItem>? VariableBindings,
    IReadOnlyList<long>? DatabaseBindingIds,
    IReadOnlyList<long>? VariableBindingIds,
    long? ModelConfigId,
    string? ModelName,
    float? Temperature,
    int? MaxTokens,
    long? DefaultWorkflowId,
    string? DefaultWorkflowName,
    string Status,
    long CreatorId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt,
    int PublishVersion,
    bool EnableMemory,
    bool EnableShortTermMemory,
    bool EnableLongTermMemory,
    int LongTermMemoryTopK,
    IReadOnlyList<long>? KnowledgeBaseIds,
    IReadOnlyList<AgentPluginBindingItem>? PluginBindings,
    string? PublishedConnectorConfigJson = null,
    long? WorkspaceId = null);

public sealed record AgentCreateRequest(
    string Name,
    string? Description,
    string? AvatarUrl,
    string? SystemPrompt,
    string? PersonaMarkdown,
    string? Goals,
    string? ReplyLogic,
    string? OutputFormat,
    string? Constraints,
    string? OpeningMessage,
    IReadOnlyList<string>? PresetQuestions,
    IReadOnlyList<AgentKnowledgeBindingInput>? KnowledgeBindings,
    IReadOnlyList<AgentDatabaseBindingInput>? DatabaseBindings,
    IReadOnlyList<AgentVariableBindingInput>? VariableBindings,
    IReadOnlyList<long>? KnowledgeBaseIds,
    IReadOnlyList<long>? DatabaseBindingIds,
    IReadOnlyList<long>? VariableBindingIds,
    long? ModelConfigId,
    string? ModelName,
    float? Temperature,
    int? MaxTokens,
    long? DefaultWorkflowId,
    string? DefaultWorkflowName,
    bool? EnableMemory,
    bool? EnableShortTermMemory,
    bool? EnableLongTermMemory,
    int? LongTermMemoryTopK,
    long? WorkspaceId = null);

public sealed record AgentUpdateRequest(
    string Name,
    string? Description,
    string? AvatarUrl,
    string? SystemPrompt,
    string? PersonaMarkdown,
    string? Goals,
    string? ReplyLogic,
    string? OutputFormat,
    string? Constraints,
    string? OpeningMessage,
    IReadOnlyList<string>? PresetQuestions,
    IReadOnlyList<AgentKnowledgeBindingInput>? KnowledgeBindings,
    IReadOnlyList<AgentDatabaseBindingInput>? DatabaseBindings,
    IReadOnlyList<AgentVariableBindingInput>? VariableBindings,
    IReadOnlyList<long>? DatabaseBindingIds,
    IReadOnlyList<long>? VariableBindingIds,
    long? ModelConfigId,
    string? ModelName,
    float? Temperature,
    int? MaxTokens,
    long? DefaultWorkflowId,
    string? DefaultWorkflowName,
    bool? EnableMemory,
    bool? EnableShortTermMemory,
    bool? EnableLongTermMemory,
    int? LongTermMemoryTopK,
    IReadOnlyList<long>? KnowledgeBaseIds,
    IReadOnlyList<AgentPluginBindingInput>? PluginBindings,
    long? WorkspaceId = null);

public sealed record AgentPluginBindingItem(
    long PluginId,
    int SortOrder,
    bool IsEnabled,
    string ToolConfigJson,
    IReadOnlyList<AgentPluginToolBindingItem>? ToolBindings = null);

public sealed record AgentPluginBindingInput(
    long PluginId,
    int SortOrder,
    bool IsEnabled,
    string? ToolConfigJson,
    IReadOnlyList<AgentPluginToolBindingInput>? ToolBindings = null);

public sealed record AgentKnowledgeBindingItem(
    long KnowledgeBaseId,
    bool IsEnabled,
    string InvokeMode,
    int TopK,
    double? ScoreThreshold,
    IReadOnlyList<string> EnabledContentTypes,
    string? RewriteQueryTemplate);

public sealed record AgentKnowledgeBindingInput(
    long KnowledgeBaseId,
    bool IsEnabled,
    string InvokeMode,
    int TopK,
    double? ScoreThreshold,
    IReadOnlyList<string>? EnabledContentTypes,
    string? RewriteQueryTemplate);

public sealed record AgentDatabaseBindingItem(
    long DatabaseId,
    string? Alias,
    string AccessMode,
    IReadOnlyList<string> TableAllowlist,
    bool IsDefault);

public sealed record AgentDatabaseBindingInput(
    long DatabaseId,
    string? Alias,
    string AccessMode,
    IReadOnlyList<string>? TableAllowlist,
    bool IsDefault);

public sealed record AgentVariableBindingItem(
    long VariableId,
    string? Alias,
    bool IsRequired,
    string? DefaultValueOverride);

public sealed record AgentVariableBindingInput(
    long VariableId,
    string? Alias,
    bool IsRequired,
    string? DefaultValueOverride);

public sealed record AgentPluginToolBindingItem(
    long ApiId,
    bool IsEnabled,
    int TimeoutSeconds,
    string FailurePolicy,
    IReadOnlyList<AgentPluginParameterBindingItem> ParameterBindings);

public sealed record AgentPluginToolBindingInput(
    long ApiId,
    bool IsEnabled,
    int TimeoutSeconds,
    string FailurePolicy,
    IReadOnlyList<AgentPluginParameterBindingInput>? ParameterBindings);

public sealed record AgentPluginParameterBindingItem(
    string ParameterName,
    string ValueSource,
    string? LiteralValue,
    string? VariableKey);

public sealed record AgentPluginParameterBindingInput(
    string ParameterName,
    string ValueSource,
    string? LiteralValue,
    string? VariableKey);

public sealed record WorkflowBindingDto(
    long? WorkflowId,
    string? WorkflowName);

public sealed record WorkflowBindingUpdateRequest(
    long? WorkflowId);
