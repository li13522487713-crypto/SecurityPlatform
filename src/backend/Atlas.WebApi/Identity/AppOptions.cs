using System;

namespace Atlas.WebApi.Identity;

public sealed class AppOptions
{
    public string HeaderName { get; init; } = "X-App-Id";
    public string DefaultAppId { get; init; } = "SecurityPlatform";
    public IReadOnlyList<AppClientTypeMapping> ClientTypeMappings { get; init; }
        = Array.Empty<AppClientTypeMapping>();
}

public sealed record AppClientTypeMapping(string ClientType, string AppId);
