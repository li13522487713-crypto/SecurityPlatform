using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

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

    /// <summary>父流程实例 ID</summary>
    public long? ParentInstanceId { get; private set; }

    /// <summary>优先级</summary>
    public int? Priority { get; private set; }

    /// <summary>流程编号</summary>
    public string? InstanceNo { get; private set; }

    /// <summary>当前节点名称</summary>
    public string? CurrentNodeName { get; private set; }

    /// <summary>乐观并发版本号（SqlSugar 自动校验）</summary>
    [SugarColumn(IsEnableUpdateVersionValidation = true)]
    public long RowVersion { get; private set; }

    public void MarkCompleted(DateTimeOffset now)
    {
        if (Status != ApprovalInstanceStatus.Running)
        {
            throw new InvalidOperationException($"Cannot complete instance in '{Status}' status. Expected: Running.");
        }
        Status = ApprovalInstanceStatus.Completed;
        EndedAt = now;
    }

    public void MarkRejected(DateTimeOffset now)
    {
        if (Status != ApprovalInstanceStatus.Running)
        {
            throw new InvalidOperationException($"Cannot reject instance in '{Status}' status. Expected: Running.");
        }
        Status = ApprovalInstanceStatus.Rejected;
        EndedAt = now;
    }

    public void MarkCanceled(DateTimeOffset now)
    {
        if (Status != ApprovalInstanceStatus.Running)
        {
            throw new InvalidOperationException($"Cannot cancel instance in '{Status}' status. Expected: Running.");
        }
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
            Status == ApprovalInstanceStatus.Canceled ||
            Status == ApprovalInstanceStatus.Terminated)
        {
            Status = ApprovalInstanceStatus.Running;
            EndedAt = null;
        }
    }

    public void Suspend()
    {
        if (Status == ApprovalInstanceStatus.Running)
        {
            Status = ApprovalInstanceStatus.Suspended;
        }
    }

    public void Activate()
    {
        if (Status == ApprovalInstanceStatus.Suspended || Status == ApprovalInstanceStatus.Draft)
        {
            Status = ApprovalInstanceStatus.Running;
        }
    }

    public void SaveAsDraft()
    {
        Status = ApprovalInstanceStatus.Draft;
    }

    public void Terminate(DateTimeOffset now)
    {
        if (Status == ApprovalInstanceStatus.Running || Status == ApprovalInstanceStatus.Suspended)
        {
            Status = ApprovalInstanceStatus.Terminated;
            EndedAt = now;
        }
    }

    public void SetParentInstanceId(long? parentInstanceId)
    {
        ParentInstanceId = parentInstanceId;
    }

    public void SetPriority(int? priority)
    {
        Priority = priority;
    }

    public void SetInstanceNo(string? instanceNo)
    {
        InstanceNo = instanceNo;
    }

    public void SetCurrentNodeName(string? currentNodeName)
    {
        CurrentNodeName = currentNodeName;
    }
}
