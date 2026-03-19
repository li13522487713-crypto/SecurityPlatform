using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批通知重试记录（外部渠道发送失败时持久化，由后台 Job 重试）
/// </summary>
public sealed class ApprovalNotificationRetry : TenantEntity
{
    public ApprovalNotificationRetry()
        : base(TenantId.Empty)
    {
        Title = string.Empty;
        Content = string.Empty;
    }

    public ApprovalNotificationRetry(
        TenantId tenantId,
        long recipientUserId,
        ApprovalNotificationChannel channel,
        string title,
        string content,
        long id,
        int maxRetries = 5)
        : base(tenantId)
    {
        Id = id;
        RecipientUserId = recipientUserId;
        Channel = channel;
        Title = title;
        Content = content;
        RetryCount = 0;
        MaxRetries = maxRetries;
        Status = NotificationRetryStatus.Pending;
        NextRetryAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
        LastError = null;
    }

    /// <summary>收件人用户ID</summary>
    public long RecipientUserId { get; private set; }

    /// <summary>通知渠道</summary>
    public ApprovalNotificationChannel Channel { get; private set; }

    /// <summary>通知标题</summary>
    public string Title { get; private set; }

    /// <summary>通知内容</summary>
    public string Content { get; private set; }

    /// <summary>已重试次数</summary>
    public int RetryCount { get; private set; }

    /// <summary>最大重试次数</summary>
    public int MaxRetries { get; private set; }

    /// <summary>重试状态</summary>
    public NotificationRetryStatus Status { get; private set; }

    /// <summary>下次重试时间</summary>
    public DateTimeOffset NextRetryAt { get; private set; }

    /// <summary>最后一次错误信息</summary>
    public string? LastError { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// 标记发送成功
    /// </summary>
    public void MarkCompleted()
    {
        Status = NotificationRetryStatus.Completed;
    }

    /// <summary>
    /// 记录重试失败并计算下次重试时间（指数退避：2^retryCount 分钟）
    /// </summary>
    public void RecordFailure(string errorMessage)
    {
        RetryCount++;
        LastError = errorMessage;

        if (RetryCount >= MaxRetries)
        {
            Status = NotificationRetryStatus.Failed;
        }
        else
        {
            // 指数退避：1min, 2min, 4min, 8min, 16min...
            var delayMinutes = Math.Pow(2, RetryCount);
            NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(delayMinutes);
        }
    }
}

/// <summary>
/// 通知重试状态
/// </summary>
public enum NotificationRetryStatus
{
    /// <summary>待重试</summary>
    Pending = 0,

    /// <summary>已完成</summary>
    Completed = 1,

    /// <summary>已失败（超过最大重试次数）</summary>
    Failed = 2
}
