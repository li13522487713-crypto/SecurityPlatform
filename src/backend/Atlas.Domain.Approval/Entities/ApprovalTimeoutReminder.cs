using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 超时提醒记录（对应 AntFlow 的 BpmnTimeoutReminder）
/// </summary>
public sealed class ApprovalTimeoutReminder : TenantEntity
{
    public ApprovalTimeoutReminder()
        : base(TenantId.Empty)
    {
        InstanceId = 0;
        TaskId = 0;
        NodeId = string.Empty;
    }

    public ApprovalTimeoutReminder(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        string nodeId,
        ReminderType reminderType,
        long recipientUserId,
        DateTimeOffset expectedCompleteTime,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        TaskId = taskId;
        NodeId = nodeId;
        ReminderType = reminderType;
        RecipientUserId = recipientUserId;
        ExpectedCompleteTime = expectedCompleteTime;
        ReminderCount = 0;
        LastReminderAt = null;
        IsCompleted = false;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>任务 ID（可选，节点级超时可能没有具体任务）</summary>
    public long? TaskId { get; private set; }

    /// <summary>节点 ID</summary>
    public string NodeId { get; private set; }

    /// <summary>提醒类型</summary>
    public ReminderType ReminderType { get; private set; }

    /// <summary>收件人用户 ID</summary>
    public long RecipientUserId { get; private set; }

    /// <summary>预期完成时间</summary>
    public DateTimeOffset ExpectedCompleteTime { get; private set; }

    /// <summary>已提醒次数</summary>
    public int ReminderCount { get; private set; }

    /// <summary>最后提醒时间</summary>
    public DateTimeOffset? LastReminderAt { get; private set; }

    /// <summary>是否已完成（任务已完成或流程已结束）</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordReminder(DateTimeOffset now)
    {
        ReminderCount++;
        LastReminderAt = now;
    }

    public void MarkCompleted()
    {
        IsCompleted = true;
    }
}
