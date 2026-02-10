using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批委派记录
/// </summary>
public sealed class ApprovalDelegateRecord : TenantEntity
{
    public ApprovalDelegateRecord() : base(TenantId.Empty) { }

    public ApprovalDelegateRecord(
        TenantId tenantId,
        long taskId,
        long delegatorUserId,
        long delegateeUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        TaskId = taskId;
        DelegatorUserId = delegatorUserId;
        DelegateeUserId = delegateeUserId;
        Status = 0; // 0=Pending, 1=Completed, 2=Cancelled
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>原任务ID</summary>
    public long TaskId { get; private set; }

    /// <summary>委派人ID</summary>
    public long DelegatorUserId { get; private set; }

    /// <summary>被委派人ID</summary>
    public long DelegateeUserId { get; private set; }

    /// <summary>状态</summary>
    public int Status { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>完成时间</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = 1;
        CompletedAt = now;
    }

    public void MarkCancelled(DateTimeOffset now)
    {
        Status = 2;
        CompletedAt = now;
    }
}
