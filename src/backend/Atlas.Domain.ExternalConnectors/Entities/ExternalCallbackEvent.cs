using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 入站 webhook 原始事件落库，支撑幂等校验、失败重试、死信投递。
/// 与现有 ApprovalExternalCallbackRecord（出站）形成对偶。
/// </summary>
public sealed class ExternalCallbackEvent : TenantEntity
{
    public ExternalCallbackEvent()
        : base(TenantId.Empty)
    {
        Topic = string.Empty;
        IdempotencyKey = string.Empty;
        RawPayloadEncrypted = string.Empty;
        SignatureSnapshot = string.Empty;
    }

    public ExternalCallbackEvent(
        TenantId tenantId,
        long id,
        long providerId,
        CallbackInboxKind kind,
        string topic,
        string idempotencyKey,
        string rawPayloadEncrypted,
        string signatureSnapshot,
        DateTimeOffset receivedAt)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        Kind = kind;
        Topic = topic;
        IdempotencyKey = idempotencyKey;
        RawPayloadEncrypted = rawPayloadEncrypted;
        SignatureSnapshot = signatureSnapshot;
        Status = CallbackInboxStatus.Received;
        ReceivedAt = receivedAt;
    }

    public long ProviderId { get; private set; }

    public CallbackInboxKind Kind { get; private set; }

    public string Topic { get; private set; }

    /// <summary>幂等键（通常是 EventId 或 sp_no + status 拼接，由 provider 提供）。</summary>
    public string IdempotencyKey { get; private set; }

    /// <summary>密文存储的原始 payload（XML 或 JSON），便于事后审计与回放。</summary>
    public string RawPayloadEncrypted { get; private set; }

    /// <summary>外部签名头与解码参数快照，用于排查"无法验签"问题。</summary>
    public string SignatureSnapshot { get; private set; }

    public CallbackInboxStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public DateTimeOffset? NextRetryAt { get; private set; }

    public string? LastError { get; private set; }

    public void MarkVerified(DateTimeOffset now)
    {
        Status = CallbackInboxStatus.Verified;
        ProcessedAt = now;
    }

    public void MarkProcessed(DateTimeOffset now)
    {
        Status = CallbackInboxStatus.Processed;
        ProcessedAt = now;
    }

    public void MarkDuplicated(DateTimeOffset now)
    {
        Status = CallbackInboxStatus.Duplicated;
        ProcessedAt = now;
    }

    public void MarkFailed(string error, int retryDelaySeconds, DateTimeOffset now, int maxRetry)
    {
        RetryCount++;
        LastError = error;
        if (RetryCount >= maxRetry)
        {
            Status = CallbackInboxStatus.DeadLetter;
            NextRetryAt = null;
        }
        else
        {
            Status = CallbackInboxStatus.Failed;
            NextRetryAt = now.AddSeconds(retryDelaySeconds);
        }
    }
}
