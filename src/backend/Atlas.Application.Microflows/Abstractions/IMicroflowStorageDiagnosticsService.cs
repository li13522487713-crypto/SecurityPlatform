using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowStorageDiagnosticsService
{
    Task<MicroflowStorageHealthDto> GetHealthAsync(CancellationToken cancellationToken);
}

public sealed record MicroflowStorageHealthDto
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("tables")]
    public IReadOnlyList<MicroflowStorageTableHealthDto> Tables { get; init; } = Array.Empty<MicroflowStorageTableHealthDto>();
}

public sealed record MicroflowStorageTableHealthDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("exists")]
    public bool Exists { get; init; }
}
