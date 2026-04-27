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
}
