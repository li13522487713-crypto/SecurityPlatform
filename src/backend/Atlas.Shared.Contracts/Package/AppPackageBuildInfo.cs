namespace Atlas.Shared.Contracts.Package;

public sealed record AppPackageBuildInfo
{
    public string BuildNumber { get; init; } = string.Empty;

    public string CommitSha { get; init; } = string.Empty;

    public string BuiltBy { get; init; } = string.Empty;

    public DateTimeOffset BuiltAt { get; init; } = DateTimeOffset.UtcNow;
}
