using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public sealed record MicroflowHealthDto
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName("service")]
    public string Service { get; init; } = "microflows";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "backend-skeleton";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }

    [JsonPropertyName("environment")]
    public string Environment { get; init; } = string.Empty;

    [JsonPropertyName("checks")]
    public IReadOnlyList<MicroflowHealthCheckDto> Checks { get; init; } = Array.Empty<MicroflowHealthCheckDto>();
}

public sealed record MicroflowHealthCheckDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "healthy";

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;
}
