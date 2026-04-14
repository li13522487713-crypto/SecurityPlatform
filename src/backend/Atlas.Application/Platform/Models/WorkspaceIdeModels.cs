using Atlas.Core.Models;

namespace Atlas.Application.Platform.Models;

public sealed record WorkspaceIdeSummaryResponse(
    int AppCount,
    int AgentCount,
    int WorkflowCount,
    int ChatflowCount,
    int PluginCount,
    int KnowledgeBaseCount,
    int DatabaseCount,
    int FavoriteCount,
    int RecentCount);

public sealed record WorkspaceIdePendingPublishItem(
    string ResourceType,
    string ResourceId,
    string ResourceName,
    DateTime UpdatedAt);

public sealed record WorkspaceIdeDashboardStatsResponse(
    int AgentCount,
    int AppCount,
    int WorkflowCount,
    int EnabledModelCount,
    int PluginCount,
    int KnowledgeBaseCount,
    IReadOnlyList<WorkspaceIdePendingPublishItem> PendingPublishItems,
    IReadOnlyList<WorkspaceIdeResourceCardResponse> RecentActivities);

public sealed record WorkspaceIdeResourceQueryRequest(
    string? Keyword,
    string? ResourceType,
    bool FavoriteOnly,
    int PageIndex,
    int PageSize);

public sealed record WorkspaceIdeResourceCardResponse(
    string ResourceType,
    string ResourceId,
    string Name,
    string? Description,
    string? Icon,
    string Status,
    string PublishStatus,
    DateTime UpdatedAt,
    bool IsFavorite,
    DateTime? LastOpenedAt,
    DateTime? LastEditedAt,
    string EntryRoute,
    string? Badge,
    string? LinkedWorkflowId = null);

public sealed record WorkspaceIdeFavoriteUpdateRequest(
    bool IsFavorite);

public sealed record WorkspaceIdeActivityCreateRequest(
    string ResourceType,
    long ResourceId,
    string ResourceTitle,
    string EntryRoute);

public sealed record WorkspaceIdeCreateAppRequest(
    string Name,
    string? Description,
    string? Icon);

public sealed record WorkspaceIdeCreateAppResult(
    string AppId,
    string WorkflowId,
    string EntryRoute);

public sealed record WorkspaceIdeResourceReferenceResponse(
    string ReferrerType,
    string ReferrerId,
    string ReferrerName,
    string BindingField);

public sealed record WorkspaceIdePublishCenterItemResponse(
    string ResourceType,
    string ResourceId,
    string ResourceName,
    int CurrentVersion,
    int DraftVersion,
    DateTime? LastPublishedAt,
    string Status,
    string? ApiEndpoint,
    string? EmbedToken);
