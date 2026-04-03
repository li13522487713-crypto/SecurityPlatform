namespace Atlas.Shared.Contracts.Process;

public sealed record AppInstanceConfig
{
    public string AppKey { get; init; } = string.Empty;

    public string InstanceId { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = "Production";

    public string BaseUrl { get; init; } = string.Empty;

    public string LoginUrl { get; init; } = string.Empty;

    public int Port { get; init; }
}
