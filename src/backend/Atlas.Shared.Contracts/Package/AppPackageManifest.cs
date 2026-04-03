namespace Atlas.Shared.Contracts.Package;

public sealed record AppPackageManifest
{
    public string AppKey { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string ArtifactId { get; init; } = string.Empty;

    public DateTimeOffset BuiltAt { get; init; } = DateTimeOffset.UtcNow;
}
