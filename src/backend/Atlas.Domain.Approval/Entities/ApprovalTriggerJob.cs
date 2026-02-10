using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批触发器任务
/// </summary>
public sealed class ApprovalTriggerJob : TenantEntity
{
    public ApprovalTriggerJob() : base(TenantId.Empty) { }

    public ApprovalTriggerJob(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        string triggerType,
        DateTimeOffset scheduledAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        NodeId = nodeId;
        TriggerType = triggerType;
        ScheduledAt = scheduledAt;
        Status = 0; // 0=Pending, 1=Executed, 2=Cancelled, 3=Failed
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>流程实例ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>节点ID</summary>
    public string NodeId { get; private set; } = string.Empty;

    /// <summary>触发器类型</summary>
    public string TriggerType { get; private set; } = string.Empty;

    /// <summary>计划执行时间</summary>
    public DateTimeOffset ScheduledAt { get; private set; }

    /// <summary>状态</summary>
    public int Status { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>执行时间</summary>
    public DateTimeOffset? ExecutedAt { get; private set; }

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; private set; }

    public void MarkExecuted(DateTimeOffset now)
    {
        Status = 1;
        ExecutedAt = now;
    }

    public void MarkFailed(DateTimeOffset now, string error)
    {
        Status = 3;
        ExecutedAt = now;
        ErrorMessage = error;
    }

    public void MarkCancelled(DateTimeOffset now)
    {
        Status = 2;
        ExecutedAt = now;
    }
}
