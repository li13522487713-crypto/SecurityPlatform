using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批沟通记录
/// </summary>
public sealed class ApprovalCommunicationRecord : TenantEntity
{
    public ApprovalCommunicationRecord() : base(TenantId.Empty) { }

    public ApprovalCommunicationRecord(
        TenantId tenantId,
        long instanceId,
        long taskId,
        long senderUserId,
        long recipientUserId,
        string content,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        TaskId = taskId;
        SenderUserId = senderUserId;
        RecipientUserId = recipientUserId;
        Content = content;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>流程实例ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>任务ID</summary>
    public long TaskId { get; private set; }

    /// <summary>发起人ID</summary>
    public long SenderUserId { get; private set; }

    /// <summary>接收人ID</summary>
    public long RecipientUserId { get; private set; }

    /// <summary>沟通内容</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>回复内容</summary>
    public string? ReplyContent { get; private set; }

    /// <summary>回复时间</summary>
    public DateTimeOffset? RepliedAt { get; private set; }

    public void Reply(string content, DateTimeOffset now)
    {
        ReplyContent = content;
        RepliedAt = now;
    }
}
