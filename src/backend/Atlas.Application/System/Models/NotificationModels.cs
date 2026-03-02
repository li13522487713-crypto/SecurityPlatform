using Atlas.Core.Models;

namespace Atlas.Application.System.Models;

// ===== DTOs =====

public sealed record NotificationDto(
    long Id,
    string Title,
    string Content,
    string NoticeType,
    int Priority,
    long PublisherId,
    string PublisherName,
    DateTimeOffset PublishedAt,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record UserNotificationDto(
    long UserNotificationId,
    long NotificationId,
    string Title,
    string Content,
    string NoticeType,
    int Priority,
    DateTimeOffset PublishedAt,
    bool IsRead,
    DateTimeOffset? ReadAt);

// ===== Request Models =====

public sealed record NotificationCreateRequest(
    string Title,
    string Content,
    string NoticeType,
    int Priority,
    DateTimeOffset? ExpiresAt);

public sealed record NotificationUpdateRequest(
    string Title,
    string Content,
    string NoticeType,
    int Priority,
    DateTimeOffset? ExpiresAt);

public sealed record NotificationPagedQuery(
    int PageIndex,
    int PageSize,
    string? Title,
    string? NoticeType,
    bool? IsActive);

public sealed record UserNotificationPagedQuery(
    int PageIndex,
    int PageSize,
    bool? IsRead);
