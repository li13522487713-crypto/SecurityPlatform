using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Contracts;

public sealed record MicroflowApiFieldError
{
    [JsonPropertyName("fieldPath")]
    public string FieldPath { get; init; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
