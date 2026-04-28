using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Contracts;

public sealed record MicroflowApiError
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = MicroflowApiErrorCode.MicroflowUnknownError;

    [JsonPropertyName("message")]
    public string Message { get; init; } = "微流服务发生未知错误。";

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    [JsonPropertyName("fieldErrors")]
    public IReadOnlyList<MicroflowApiFieldError> FieldErrors { get; init; } = Array.Empty<MicroflowApiFieldError>();

    [JsonPropertyName("validationIssues")]
    public IReadOnlyList<MicroflowValidationIssueDto> ValidationIssues { get; init; } = Array.Empty<MicroflowValidationIssueDto>();

    [JsonPropertyName("retryable")]
    public bool Retryable { get; init; }

    [JsonPropertyName("httpStatus")]
    public int? HttpStatus { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }
}
