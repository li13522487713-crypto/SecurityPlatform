namespace Atlas.Application.AiPlatform.Models;

public sealed record OpenApiCallLogCreateRequest(
    long? ProjectId,
    string? AppId,
    long UserId,
    string ApiName,
    string HttpMethod,
    string RequestPath,
    bool IsSuccess,
    int StatusCode,
    string? ErrorCode,
    long DurationMs,
    string TraceId,
    DateTime CreatedAt);

public sealed record OpenApiCallStatsSummary(
    long? ProjectId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    long TotalCalls,
    long SuccessCalls,
    long FailedCalls,
    decimal SuccessRate,
    double AverageDurationMs,
    long MaxDurationMs);
