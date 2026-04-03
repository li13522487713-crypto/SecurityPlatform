namespace Atlas.Shared.Contracts.Health;

public sealed record AppLoginEntry
{
    public string AppKey { get; init; } = string.Empty;

    public string LoginUrl { get; init; } = string.Empty;
}
