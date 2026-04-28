using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Contracts;

public sealed class MicroflowApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("error")]
    public MicroflowApiError? Error { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    public static MicroflowApiResponse<T> Ok(T data, string? traceId = null)
    {
        return new MicroflowApiResponse<T>
        {
            Success = true,
            Data = data,
            TraceId = traceId,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public static MicroflowApiResponse<T> Fail(MicroflowApiError error, string? traceId = null)
    {
        return new MicroflowApiResponse<T>
        {
            Success = false,
            Error = error with { TraceId = error.TraceId ?? traceId },
            TraceId = traceId ?? error.TraceId,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
