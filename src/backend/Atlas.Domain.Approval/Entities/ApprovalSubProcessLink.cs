using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 子流程关联记录
/// </summary>
public sealed class ApprovalSubProcessLink : TenantEntity
{
    public ApprovalSubProcessLink() : base(TenantId.Empty) { }

    public ApprovalSubProcessLink(
        TenantId tenantId,
        long parentInstanceId,
        string parentNodeId,
        long childInstanceId,
        long childProcessId,
        bool isAsync,
        long id)
        : base(tenantId)
    {
        Id = id;
        ParentInstanceId = parentInstanceId;
        ParentNodeId = parentNodeId;
        ChildInstanceId = childInstanceId;
        ChildProcessId = childProcessId;
        IsAsync = isAsync;
        Status = 0; // 0=Running, 1=Completed
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>父流程实例ID</summary>
    public long ParentInstanceId { get; private set; }

    /// <summary>父流程节点ID（触发子流程的节点）</summary>
    public string ParentNodeId { get; private set; } = string.Empty;

    /// <summary>子流程实例ID</summary>
    public long ChildInstanceId { get; private set; }

    /// <summary>子流程定义ID</summary>
    public long ChildProcessId { get; private set; }

    /// <summary>是否异步</summary>
    public bool IsAsync { get; private set; }

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
}
