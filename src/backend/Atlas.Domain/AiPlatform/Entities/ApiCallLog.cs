using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class ApiCallLog : TenantEntity
{
    public ApiCallLog()
        : base(TenantId.Empty)
    {
        ApiName = string.Empty;
        HttpMethod = string.Empty;
        RequestPath = string.Empty;
        TraceId = string.Empty;
        ErrorCode = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public ApiCallLog(
        TenantId tenantId,
        long id,
        long? projectId,
        string? appId,
        long userId,
        string apiName,
        string httpMethod,
        string requestPath,
        bool isSuccess,
        int statusCode,
        string? errorCode,
        long durationMs,
        string traceId,
        DateTime createdAt)
        : base(tenantId)
    {
        Id = id;
        ProjectId = projectId;
        AppId = appId;
        UserId = userId;
        ApiName = apiName;
        HttpMethod = httpMethod;
        RequestPath = requestPath;
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        ErrorCode = errorCode ?? string.Empty;
        DurationMs = durationMs;
        TraceId = traceId;
        CreatedAt = createdAt;
    }

    public long? ProjectId { get; private set; }
    public string? AppId { get; private set; }
    public long UserId { get; private set; }
    public string ApiName { get; private set; }
    public string HttpMethod { get; private set; }
    public string RequestPath { get; private set; }
    public bool IsSuccess { get; private set; }
    public int StatusCode { get; private set; }
    public string ErrorCode { get; private set; }
    public long DurationMs { get; private set; }
    public string TraceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
