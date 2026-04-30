using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Runtime;

public sealed record RuntimeValueRef
{
    [JsonPropertyName("refId")]
    public string RefId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("refKind")]
    public string RefKind { get; init; } = "blob";

    [JsonPropertyName("contentType")]
    public string? ContentType { get; init; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    [JsonPropertyName("preview")]
    public string? Preview { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }
}
