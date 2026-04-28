using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Coze.Models;

/// <summary>
/// 工作空间发布渠道（PRD 04-4.6）。
/// </summary>
public sealed record WorkspacePublishChannelDto(
    string Id,
    string WorkspaceId,
    string Name,
    string Type,
    string Status,
    string AuthStatus,
    string? Description,
    IReadOnlyList<string> SupportedTargets,
    DateTimeOffset? LastSyncAt,
    DateTimeOffset CreatedAt);

public sealed record WorkspacePublishChannelCreateRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Name,
    [Required, StringLength(32)] string Type,
    [StringLength(512)] string? Description,
    IReadOnlyList<string>? SupportedTargets);

public sealed record WorkspacePublishChannelUpdateRequest(
    [StringLength(64, MinimumLength = 1)] string? Name,
    [StringLength(512)] string? Description,
    [StringLength(16)] string? Status,
    IReadOnlyList<string>? SupportedTargets);

public sealed record PublishChannelCatalogItemDto(
    string ChannelKey,
    string DisplayName,
    string? PublishChannelType,
    string? CredentialKind,
    bool AllowDraft,
    bool AllowOnline);
