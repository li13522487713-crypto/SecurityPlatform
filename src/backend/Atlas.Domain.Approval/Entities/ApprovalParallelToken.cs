using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 并行网关 Token 跟踪（用于并行分支的汇聚）
/// </summary>
[SugarIndex(
    "IX_ApprovalParallelToken_TenantId_InstanceId_GatewayNodeId",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(InstanceId), OrderByType.Asc,
    nameof(GatewayNodeId), OrderByType.Asc)]
public sealed class ApprovalParallelToken : TenantEntity
{
    public ApprovalParallelToken()
        : base(TenantId.Empty)
    {
        GatewayNodeId = string.Empty;
        BranchNodeId = string.Empty;
    }

    public ApprovalParallelToken(
        TenantId tenantId,
        long instanceId,
        string gatewayNodeId,
        string branchNodeId,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        GatewayNodeId = gatewayNodeId;
        BranchNodeId = branchNodeId;
        Status = ParallelTokenStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        CompletedAt = null;
    }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>并行网关节点 ID（汇聚网关）</summary>
    public string GatewayNodeId { get; private set; }

    /// <summary>分支节点 ID（从并行网关分出的路径）</summary>
    public string BranchNodeId { get; private set; }

    /// <summary>Token 状态</summary>
    public ParallelTokenStatus Status { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>完成时间</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = ParallelTokenStatus.Completed;
        CompletedAt = now;
    }
}

/// <summary>
/// 并行 Token 状态
/// </summary>
public enum ParallelTokenStatus
{
    /// <summary>活跃（等待完成）</summary>
    Active = 0,

    /// <summary>已完成</summary>
    Completed = 1
}
