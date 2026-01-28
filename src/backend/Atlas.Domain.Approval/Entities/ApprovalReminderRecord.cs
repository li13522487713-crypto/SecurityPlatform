using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 催办记录（对应 AntFlow 的 BpmnApproveRemind）
/// </summary>
public sealed class ApprovalReminderRecord : TenantEntity
{
    public ApprovalReminderRecord()
        : base(TenantId.Empty)
    {
        InstanceId = 0;
        TaskId = 0;
        ReminderMessage = string.Empty;
    }

    public ApprovalReminderRecord(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long reminderUserId,
        long recipientUserId,
        string reminderMessage,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        TaskId = taskId;
        ReminderUserId = reminderUserId;
        RecipientUserId = recipientUserId;
        ReminderMessage = reminderMessage;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>任务 ID（可选）</summary>
    public long? TaskId { get; private set; }

    /// <summary>催办人用户 ID</summary>
    public long ReminderUserId { get; private set; }

    /// <summary>被催办人用户 ID</summary>
    public long RecipientUserId { get; private set; }

    /// <summary>催办消息</summary>
    public string ReminderMessage { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
