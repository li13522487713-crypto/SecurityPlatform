using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record TeamAgentMemberInput(
    long? AgentId,
    string RoleName,
    string? Responsibility,
    string? Alias,
    int SortOrder,
    bool IsEnabled,
    string? PromptPrefix,
    IReadOnlyList<string>? CapabilityTags);

public sealed record TeamAgentMemberItem(
    long? AgentId,
    string RoleName,
    string? Responsibility,
    string? Alias,
    int SortOrder,
    bool IsEnabled,
    string? PromptPrefix,
    IReadOnlyList<string> CapabilityTags,
    string BindingState);

public sealed record TeamAgentCreateRequest(
    string Name,
    string? Description,
    TeamAgentMode TeamMode,
    IReadOnlyList<string>? CapabilityTags,
    string? DefaultEntrySkill,
    IReadOnlyList<string>? BoundDataAssets,
    IReadOnlyList<TeamAgentMemberInput> Members,
    string? SchemaConfigJson);

public sealed record TeamAgentUpdateRequest(
    string Name,
    string? Description,
    TeamAgentMode TeamMode,
    TeamAgentStatus? Status,
    IReadOnlyList<string>? CapabilityTags,
    string? DefaultEntrySkill,
    IReadOnlyList<string>? BoundDataAssets,
    IReadOnlyList<TeamAgentMemberInput> Members,
    string? SchemaConfigJson);

public sealed record TeamAgentListItem(
    long Id,
    string AgentType,
    string Name,
    string? Description,
    TeamAgentMode TeamMode,
    TeamAgentStatus Status,
    IReadOnlyList<string> CapabilityTags,
    int MemberCount,
    string? DefaultEntrySkill,
    int PublishVersion,
    IReadOnlyList<string> BoundDataAssets,
    string? LastRunStatus,
    string? LegacySourceType,
    string? LegacySourceId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record TeamAgentDetail(
    long Id,
    string AgentType,
    string Name,
    string? Description,
    TeamAgentMode TeamMode,
    TeamAgentStatus Status,
    IReadOnlyList<string> CapabilityTags,
    string? DefaultEntrySkill,
    int PublishVersion,
    IReadOnlyList<string> BoundDataAssets,
    string? SchemaConfigJson,
    long CreatorUserId,
    string? LegacySourceType,
    string? LegacySourceId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<TeamAgentMemberItem> Members);

public sealed record TeamAgentTemplateItem(
    string Key,
    string Name,
    string Description,
    TeamAgentMode TeamMode,
    IReadOnlyList<string> CapabilityTags,
    string DefaultEntrySkill,
    IReadOnlyList<TeamAgentMemberItem> Members);

public sealed record TeamAgentTemplateMemberBindingInput(
    string RoleName,
    long? AgentId,
    bool? IsEnabled);

public sealed record TeamAgentCreateFromTemplateRequest(
    string TemplateKey,
    string Name,
    string? Description,
    IReadOnlyList<TeamAgentTemplateMemberBindingInput>? MemberBindings);

public sealed record TeamAgentDashboardActivityItem(
    string ActivityType,
    long ResourceId,
    long TeamAgentId,
    string TeamAgentName,
    string Title,
    string Summary,
    DateTime OccurredAt);

public sealed record TeamAgentDashboardDto(
    int TotalCount,
    int TeamCount,
    int AvailableSubAgentCount,
    int RecentRunCount,
    int SchemaBuilderCount,
    IReadOnlyList<TeamAgentDashboardActivityItem> RecentActivities);

public sealed record TeamAgentConversationDto(
    long Id,
    long TeamAgentId,
    long UserId,
    string? Title,
    DateTime CreatedAt,
    DateTime? LastMessageAt,
    int MessageCount);

public sealed record TeamAgentConversationCreateRequest(
    string? Title);

public sealed record TeamAgentConversationUpdateRequest(
    string Title);

public sealed record TeamAgentMessageDto(
    long Id,
    string Role,
    string Content,
    string EventType,
    string? MemberName,
    string? Metadata,
    DateTime CreatedAt,
    bool IsContextCleared);

public sealed record TeamAgentChatRequest(
    long? ConversationId,
    string Message,
    bool? EnableRag,
    bool? GenerateSchemaDraft);

public sealed record TeamAgentChatCancelRequest(
    long ConversationId);

public sealed record TeamAgentChatResponse(
    long ConversationId,
    long ExecutionId,
    string Content,
    IReadOnlyList<TeamAgentRunEvent> Events,
    SchemaDraftDto? SchemaDraft);

public sealed record TeamAgentRunEvent(
    string EventType,
    string Data);

public sealed record TeamAgentExecutionStep(
    long StepId,
    long? AgentId,
    string AgentName,
    string RoleName,
    string? Alias,
    string InputMessage,
    string? OutputMessage,
    string Status,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record TeamAgentExecutionResult(
    long ExecutionId,
    long TeamAgentId,
    long ConversationId,
    string Status,
    string? OutputMessage,
    string? ErrorMessage,
    IReadOnlyList<TeamAgentExecutionStep> Steps,
    IReadOnlyList<TeamAgentRunEvent> Events,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record TeamAgentSchemaDraftListItem(
    long Id,
    long TeamAgentId,
    long? ConversationId,
    string Title,
    string Requirement,
    string Status,
    string ConfirmationState,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ConfirmedAt);

public sealed record TeamAgentSchemaDraftExecutionAuditItem(
    long Id,
    long DraftId,
    int Sequence,
    string Stage,
    string Action,
    string Status,
    string? ResourceKey,
    string? ResourceId,
    string? Detail,
    DateTime CreatedAt);

public sealed record TeamAgentPublicationListItem(
    long Id,
    long TeamAgentId,
    int Version,
    bool IsActive,
    string? ReleaseNote,
    long PublishedByUserId,
    DateTime PublishedAt,
    DateTime? RevokedAt);

public sealed record TeamAgentPublicationPublishRequest(
    string? ReleaseNote);

public sealed record TeamAgentPublicationPublishResult(
    long Id,
    long TeamAgentId,
    int Version,
    DateTime PublishedAt);

public sealed record TeamAgentLegacyMigrationRequest(
    IReadOnlyList<long>? LegacyIds);

public sealed record TeamAgentLegacyMigrationResult(
    int RequestedCount,
    int MigratedCount,
    IReadOnlyList<long> TeamAgentIds);

public sealed record TeamAgentLegacyMigrationStatusItem(
    long LegacyId,
    string LegacyName,
    string LegacyMode,
    string MigrationStatus,
    long? TeamAgentId,
    string? TeamAgentName,
    DateTime? MigratedAt,
    string ReplacementApi,
    string SunsetAt);

public sealed record TeamAgentLegacyMigrationStatusDto(
    int TotalCount,
    int MigratedCount,
    int PendingCount,
    IReadOnlyList<TeamAgentLegacyMigrationStatusItem> Items);

public sealed record SchemaDraftCreateRequest(
    string Requirement,
    long? ConversationId,
    string? Title = null);

public sealed record SchemaDraftUpdateRequest(
    string Title,
    string Requirement,
    SchemaDraftDto SchemaDraft,
    IReadOnlyList<SchemaDraftOpenQuestionDto>? OpenQuestions);

public sealed record SchemaDraftConfirmationRequest(
    bool Confirmed,
    string? Notes = null);

public sealed record SchemaDraftConfirmationResponse(
    long DraftId,
    string ConfirmationState,
    IReadOnlyList<string> TableKeys,
    IReadOnlyList<SchemaDraftCreatedResourceDto> Resources);

public sealed record SchemaDraftCreatedResourceDto(
    string TableKey,
    string ResourceId);

public sealed record SchemaDraftDto(
    string SchemaDraft,
    IReadOnlyList<SchemaDraftEntityDto> Entities,
    IReadOnlyList<SchemaDraftFieldDto> Fields,
    IReadOnlyList<SchemaDraftRelationDto> Relations,
    IReadOnlyList<SchemaDraftIndexDto> Indexes,
    IReadOnlyList<SchemaDraftSecurityPolicyDto> SecurityPolicies,
    IReadOnlyList<SchemaDraftOpenQuestionDto> OpenQuestions,
    string ConfirmationState);

public sealed record SchemaDraftEntityDto(
    string TableKey,
    string DisplayName,
    string? Description);

public sealed record SchemaDraftFieldDto(
    string TableKey,
    string Name,
    string DisplayName,
    string FieldType,
    bool AllowNull,
    bool IsPrimaryKey,
    bool IsAutoIncrement,
    bool IsUnique,
    int SortOrder,
    int? Length = null,
    int? Precision = null,
    int? Scale = null,
    string? DefaultValue = null);

public sealed record SchemaDraftRelationDto(
    string SourceTableKey,
    string SourceField,
    string RelatedTableKey,
    string TargetField,
    string RelationType,
    string? CascadeRule);

public sealed record SchemaDraftIndexDto(
    string TableKey,
    string Name,
    bool IsUnique,
    IReadOnlyList<string> Fields);

public sealed record SchemaDraftSecurityPolicyDto(
    string TableKey,
    string FieldName,
    string RoleCode,
    bool CanView,
    bool CanEdit);

public sealed record SchemaDraftOpenQuestionDto(
    string Code,
    string Question);

public sealed record TeamAgentSchemaDraftDetail(
    long Id,
    long TeamAgentId,
    long? ConversationId,
    string Title,
    string Requirement,
    SchemaDraftDto SchemaDraft,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ConfirmedAt);
