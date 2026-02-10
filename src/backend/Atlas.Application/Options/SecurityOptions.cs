namespace Atlas.Application.Options;

public sealed class SecurityOptions
{
    public bool EnforceHttps { get; init; } = true;

    /// <summary>
    /// Maximum concurrent active sessions per user. 0 = unlimited.
    /// When exceeded, the oldest session is revoked automatically.
    /// </summary>
    public int MaxConcurrentSessions { get; init; } = 5;
}
