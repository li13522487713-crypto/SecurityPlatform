namespace Atlas.Application.Platform.Models;

public sealed record CapabilityNavigationSuggestion(
    string? Group,
    int? Order);

public sealed record CapabilityManifestItem(
    string CapabilityKey,
    string Title,
    string Category,
    IReadOnlyList<string> HostModes,
    string? PlatformRoute,
    string? AppRoute,
    IReadOnlyList<string> RequiredPermissions,
    CapabilityNavigationSuggestion Navigation,
    bool SupportsExposure,
    IReadOnlyList<string> SupportedCommands,
    bool IsEnabled);
