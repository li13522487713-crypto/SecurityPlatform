using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批状态回写失败记录（死信队列）。
/// 当重试次数超过阈值后，回写任务从重试队列移入此实体持久化，供管理员查看和手动重试。
/// </summary>
public sealed class ApprovalWritebackFailure : TenantEntity
{
    public ApprovalWritebackFailure()
        : base(TenantId.Empty)
    {
        BusinessKey = string.Empty;
        TargetStatus = string.Empty;
        ErrorMessage = string.Empty;
    }

    public ApprovalWritebackFailure(
        TenantId tenantId,
        long id,
        string businessKey,
        string targetStatus,
        int retryCount,
        string errorMessage,
        DateTimeOffset firstFailedAt,
        DateTimeOffset lastAttemptAt)
        : base(tenantId)
    {
        Id = id;
        BusinessKey = businessKey;
        TargetStatus = targetStatus;
        RetryCount = retryCount;
        ErrorMessage = errorMessage;
        FirstFailedAt = firstFailedAt;
        LastAttemptAt = lastAttemptAt;
        IsDead = true;
    }

    public string BusinessKey { get; private set; }
    public string TargetStatus { get; private set; }
    public int RetryCount { get; private set; }
    public string ErrorMessage { get; private set; }
    public DateTimeOffset FirstFailedAt { get; private set; }
    public DateTimeOffset LastAttemptAt { get; private set; }

    /// <summary>是否已是死信（超出最大重试次数）</summary>
    public bool IsDead { get; private set; }

    /// <summary>是否已手动重试成功</summary>
    public bool IsResolved { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public void MarkResolved()
    {
        IsResolved = true;
        ResolvedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateAttempt(int retryCount, string errorMessage)
    {
        RetryCount = retryCount;
        ErrorMessage = errorMessage;
        LastAttemptAt = DateTimeOffset.UtcNow;
    }
}
