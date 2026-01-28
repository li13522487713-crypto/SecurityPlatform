using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 抄送记录（抄送节点的收件人）
/// </summary>
public sealed class ApprovalCopyRecord : TenantEntity
{
    public ApprovalCopyRecord()
        : base(TenantId.Empty)
    {
        NodeId = string.Empty;
        RecipientUserId = 0;
    }

    public ApprovalCopyRecord(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        long recipientUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        NodeId = nodeId;
        RecipientUserId = recipientUserId;
        IsRead = false;
        CreatedAt = DateTimeOffset.UtcNow;
        ReadAt = null;
    }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>抄送节点 ID</summary>
    public string NodeId { get; private set; }

    /// <summary>收件人用户 ID</summary>
    public long RecipientUserId { get; private set; }

    /// <summary>是否已读</summary>
    public bool IsRead { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>阅读时间</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkAsRead(DateTimeOffset now)
    {
        IsRead = true;
        ReadAt = now;
    }
}
