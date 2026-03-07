using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

public enum IdempotencyStatus
{
    Pending = 0,
    Completed = 1
}

 [SugarIndex(
    "UX_IdempotencyRecord_Tenant_User_Api_Key",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(ApiName), OrderByType.Asc,
    nameof(IdempotencyKey), OrderByType.Asc,
    true)]
public sealed class IdempotencyRecord : TenantEntity
{
    public IdempotencyRecord()
        : base(TenantId.Empty)
    {
        ApiName = string.Empty;
        IdempotencyKey = string.Empty;
        RequestHash = string.Empty;
        ResponseBody = string.Empty;
        ResponseContentType = "application/json";
        ResourceId = string.Empty;
        CreatedAt = DateTimeOffset.MinValue;
        ExpiresAt = DateTimeOffset.MinValue;
        Status = IdempotencyStatus.Pending;
        CompletedAt = DateTimeOffset.MinValue;
    }

    public IdempotencyRecord(
        TenantId tenantId,
        long userId,
        string apiName,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        ApiName = apiName;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Status = IdempotencyStatus.Pending;
        ResponseBody = string.Empty;
        ResponseContentType = "application/json";
        ResourceId = string.Empty;
        CompletedAt = DateTimeOffset.MinValue;
    }

    public long UserId { get; private set; }
    public string ApiName { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string RequestHash { get; private set; }
    public IdempotencyStatus Status { get; private set; }
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; }
    public string ResponseContentType { get; private set; }
    public string ResourceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public void Complete(
        int statusCode,
        string? responseBody,
        string? responseContentType,
        string? resourceId,
        DateTimeOffset completedAt)
    {
        Status = IdempotencyStatus.Completed;
        StatusCode = statusCode;
        ResponseBody = responseBody ?? string.Empty;
        ResponseContentType = string.IsNullOrWhiteSpace(responseContentType) ? "application/json" : responseContentType;
        ResourceId = resourceId ?? string.Empty;
        CompletedAt = completedAt;
    }
}
