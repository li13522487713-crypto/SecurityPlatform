namespace Atlas.Shared.Contracts.Health;

public sealed record AppIngressInfo
{
    public string AppKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public int Port { get; init; }
}
