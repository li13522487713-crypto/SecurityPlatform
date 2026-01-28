using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 外部回调记录（对应 AntFlow 的 OutSideCallBackRecord）
/// </summary>
public sealed class ApprovalExternalCallbackRecord : TenantEntity
{
    public ApprovalExternalCallbackRecord()
        : base(TenantId.Empty)
    {
        ConfigId = 0;
        InstanceId = 0;
        CallbackUrl = string.Empty;
        RequestBody = string.Empty;
        IdempotencyKey = string.Empty;
    }

    public ApprovalExternalCallbackRecord(
        TenantId tenantId,
        long configId,
        long instanceId,
        long? taskId,
        string? nodeId,
        CallbackEventType eventType,
        string callbackUrl,
        string requestBody,
        string idempotencyKey,
        long id)
        : base(tenantId)
    {
        Id = id;
        ConfigId = configId;
        InstanceId = instanceId;
        TaskId = taskId;
        NodeId = nodeId;
        EventType = eventType;
        CallbackUrl = callbackUrl;
        RequestBody = requestBody;
        IdempotencyKey = idempotencyKey;
        Status = CallbackStatus.Pending;
        RetryCount = 0;
        NextRetryAt = DateTimeOffset.UtcNow;
        LastAttemptAt = null;
        ResponseBody = null;
        ErrorMessage = null;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>回调配置 ID</summary>
    public long ConfigId { get; private set; }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>任务 ID（可选）</summary>
    public long? TaskId { get; private set; }

    /// <summary>节点 ID（可选）</summary>
    public string? NodeId { get; private set; }

    /// <summary>回调事件类型</summary>
    public CallbackEventType EventType { get; private set; }

    /// <summary>回调 URL</summary>
    public string CallbackUrl { get; private set; }

    /// <summary>请求体（JSON）</summary>
    public string RequestBody { get; private set; }

    /// <summary>幂等键（用于防止重复回调）</summary>
    public string IdempotencyKey { get; private set; }

    /// <summary>回调状态</summary>
    public CallbackStatus Status { get; private set; }

    /// <summary>重试次数</summary>
    public int RetryCount { get; private set; }

    /// <summary>下次重试时间</summary>
    public DateTimeOffset NextRetryAt { get; private set; }

    /// <summary>最后尝试时间</summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>响应体（JSON）</summary>
    public string? ResponseBody { get; private set; }

    /// <summary>错误消息</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkSending(DateTimeOffset now)
    {
        Status = CallbackStatus.Sending;
        LastAttemptAt = now;
    }

    public void MarkSuccess(string? responseBody, DateTimeOffset now)
    {
        Status = CallbackStatus.Success;
        ResponseBody = responseBody;
        LastAttemptAt = now;
    }

    public void MarkFailed(string errorMessage, int retryIntervalSeconds, DateTimeOffset now)
    {
        RetryCount++;
        Status = CallbackStatus.Failed;
        ErrorMessage = errorMessage;
        LastAttemptAt = now;
        NextRetryAt = now.AddSeconds(retryIntervalSeconds);
    }

    public void MarkCancelled()
    {
        Status = CallbackStatus.Cancelled;
    }

    public bool CanRetry(int maxRetryCount)
    {
        return Status == CallbackStatus.Failed && RetryCount < maxRetryCount;
    }
}
