namespace Atlas.Application.Platform.Models;

public sealed record NavigationProjectionScope(
    string TenantId,
    string? AppInstanceId,
    string? AppKey);

public sealed record NavigationProjectionItem(
    string Key,
    string Title,
    string Path,
    string? PermissionCode,
    int Order,
    IReadOnlyList<string> SourceRefs);

public sealed record NavigationProjectionGroup(
    string GroupKey,
    string GroupTitle,
    IReadOnlyList<NavigationProjectionItem> Items);

public sealed record NavigationProjectionResponse(
    string HostMode,
    NavigationProjectionScope Scope,
    IReadOnlyList<NavigationProjectionGroup> Groups,
    string GeneratedAt);
