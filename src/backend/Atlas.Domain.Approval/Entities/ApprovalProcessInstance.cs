using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流运行时实例
/// </summary>
public sealed class ApprovalProcessInstance : TenantEntity
{
    public ApprovalProcessInstance()
        : base(TenantId.Empty)
    {
        BusinessKey = string.Empty;
        DataJson = null;
    }

    public ApprovalProcessInstance(
        TenantId tenantId,
        long definitionId,
        string businessKey,
        long initiatorUserId,
        long id,
        string? dataJson = null)
        : base(tenantId)
    {
        Id = id;
        DefinitionId = definitionId;
        BusinessKey = businessKey;
        InitiatorUserId = initiatorUserId;
        DataJson = dataJson;
        Status = ApprovalInstanceStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
        EndedAt = null;
    }

    /// <summary>流程定义 ID</summary>
    public long DefinitionId { get; private set; }

    /// <summary>业务 key（用于关联业务数据）</summary>
    public string BusinessKey { get; private set; }

    /// <summary>发起人 ID</summary>
    public long InitiatorUserId { get; private set; }

    /// <summary>业务数据 JSON</summary>
    public string? DataJson { get; private set; }

    /// <summary>状态</summary>
    public ApprovalInstanceStatus Status { get; private set; }

    /// <summary>启动时间</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>结束时间</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>当前节点 ID</summary>
    public string? CurrentNodeId { get; private set; }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = ApprovalInstanceStatus.Completed;
        EndedAt = now;
    }

    public void MarkRejected(DateTimeOffset now)
    {
        Status = ApprovalInstanceStatus.Rejected;
        EndedAt = now;
    }

    public void MarkCanceled(DateTimeOffset now)
    {
        Status = ApprovalInstanceStatus.Canceled;
        EndedAt = now;
    }

    public void UpdateData(string? dataJson)
    {
        DataJson = dataJson;
    }

    public void SetCurrentNode(string? nodeId)
    {
        CurrentNodeId = nodeId;
    }

    /// <summary>
    /// 恢复已结束的流程到运行状态
    /// </summary>
    public void Recover(DateTimeOffset now)
    {
        if (Status == ApprovalInstanceStatus.Completed ||
            Status == ApprovalInstanceStatus.Rejected ||
            Status == ApprovalInstanceStatus.Canceled)
        {
            Status = ApprovalInstanceStatus.Running;
            EndedAt = null;
        }
    }
}
