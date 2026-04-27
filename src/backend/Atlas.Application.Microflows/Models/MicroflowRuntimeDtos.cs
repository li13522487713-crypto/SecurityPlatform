using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public sealed record TestRunMicroflowRequestDto
{
    [JsonPropertyName("schema")]
    public JsonElement? Schema { get; init; }

    [JsonPropertyName("input")]
    public IReadOnlyDictionary<string, JsonElement>? Input { get; init; }

    [JsonPropertyName("options")]
    public JsonElement? Options { get; init; }
}

public sealed record CancelMicroflowRunResponseDto
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "cancelled";
}

public sealed record MicroflowRunSessionDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("schemaId")]
    public string SchemaId { get; init; } = string.Empty;

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset? EndedAt { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "failed";

    [JsonPropertyName("input")]
    public IReadOnlyDictionary<string, JsonElement> Input { get; init; } = new Dictionary<string, JsonElement>();

    [JsonPropertyName("output")]
    public JsonElement? Output { get; init; }

    [JsonPropertyName("trace")]
    public IReadOnlyList<MicroflowTraceFrameDto> Trace { get; init; } = Array.Empty<MicroflowTraceFrameDto>();

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();

    [JsonPropertyName("variables")]
    public IReadOnlyList<MicroflowRunSessionVariableSnapshotDto> Variables { get; init; } = Array.Empty<MicroflowRunSessionVariableSnapshotDto>();
}

public sealed record MicroflowTraceFrameDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "skipped";

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }
}

public sealed record MicroflowRuntimeLogDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = "info";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

public sealed record MicroflowRunSessionVariableSnapshotDto
{
    [JsonPropertyName("frameId")]
    public string FrameId { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("variables")]
    public IReadOnlyList<MicroflowRuntimeVariableValueDto> Variables { get; init; } = Array.Empty<MicroflowRuntimeVariableValueDto>();
}

public sealed record MicroflowRuntimeVariableValueDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "Unknown";

    [JsonPropertyName("valuePreview")]
    public string ValuePreview { get; init; } = string.Empty;
}

public sealed record MicroflowRunTraceResponseDto
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("trace")]
    public IReadOnlyList<MicroflowTraceFrameDto> Trace { get; init; } = Array.Empty<MicroflowTraceFrameDto>();

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();
}
