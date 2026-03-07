using System;

namespace Atlas.WebApi.Identity;

public sealed class AppOptions
{
    public string HeaderName { get; init; } = "X-App-Id";
    public string DefaultAppId { get; init; } = "SecurityPlatform";
    public bool AllowHeaderOverrideWhenAuthenticated { get; init; }
    public bool RequireWorkspaceHeaderForAuthenticatedOverride { get; init; } = true;
    public string WorkspaceHeaderName { get; init; } = "X-App-Workspace";
    public string WorkspaceHeaderValue { get; init; } = "1";
    public bool RequireHeaderAppIdNumeric { get; init; } = true;
    public IReadOnlyList<AppClientTypeMapping> ClientTypeMappings { get; init; }
        = Array.Empty<AppClientTypeMapping>();
}

public sealed record AppClientTypeMapping(string ClientType, string AppId);
