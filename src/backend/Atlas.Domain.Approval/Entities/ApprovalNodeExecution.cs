using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批节点执行记录（用于跟踪节点执行状态和流转）
/// </summary>
[SugarIndex(
    "IX_ApprovalNodeExecution_TenantId_InstanceId",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(InstanceId), OrderByType.Asc)]
[SugarIndex(
    "IX_ApprovalNodeExecution_TenantId_InstanceId_NodeId",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(InstanceId), OrderByType.Asc,
    nameof(NodeId), OrderByType.Asc)]
public sealed class ApprovalNodeExecution : TenantEntity
{
    public ApprovalNodeExecution()
        : base(TenantId.Empty)
    {
        NodeId = string.Empty;
    }

    public ApprovalNodeExecution(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        ApprovalNodeExecutionStatus status,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        NodeId = nodeId;
        Status = status;
        StartedAt = DateTimeOffset.UtcNow;
        CompletedAt = null;
    }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>节点 ID</summary>
    public string NodeId { get; private set; }

    /// <summary>执行状态</summary>
    public ApprovalNodeExecutionStatus Status { get; private set; }

    /// <summary>开始时间</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>完成时间</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>乐观并发版本号（SqlSugar 自动校验）</summary>
    [SugarColumn(IsEnableUpdateVersionValidation = true)]
    public long RowVersion { get; private set; }

    public void MarkCompleted(DateTimeOffset now)
    {
        if (Status != ApprovalNodeExecutionStatus.Running)
        {
            // Allow idempotent completion (e.g., parallel branches reaching the same join node)
            if (Status == ApprovalNodeExecutionStatus.Completed) return;
            throw new InvalidOperationException($"Cannot complete node execution in '{Status}' status. Expected: Running.");
        }
        Status = ApprovalNodeExecutionStatus.Completed;
        CompletedAt = now;
    }

    public void MarkSkipped(DateTimeOffset now)
    {
        if (Status != ApprovalNodeExecutionStatus.Running)
        {
            throw new InvalidOperationException($"Cannot skip node execution in '{Status}' status. Expected: Running.");
        }
        Status = ApprovalNodeExecutionStatus.Skipped;
        CompletedAt = now;
    }
}

/// <summary>
/// 节点执行状态
/// </summary>
public enum ApprovalNodeExecutionStatus
{
    /// <summary>执行中</summary>
    Running = 0,

    /// <summary>已完成</summary>
    Completed = 1,

    /// <summary>已跳过</summary>
    Skipped = 2
}
