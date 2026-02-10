using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批代理人配置
/// </summary>
public sealed class ApprovalAgentConfig : TenantEntity
{
    public ApprovalAgentConfig() : base(TenantId.Empty) { }

    public ApprovalAgentConfig(
        TenantId tenantId,
        long agentUserId,
        long principalUserId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        long id)
        : base(tenantId)
    {
        Id = id;
        AgentUserId = agentUserId;
        PrincipalUserId = principalUserId;
        StartTime = startTime;
        EndTime = endTime;
        IsEnabled = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>代理人ID</summary>
    public long AgentUserId { get; private set; }

    /// <summary>被代理人ID（委托人）</summary>
    public long PrincipalUserId { get; private set; }

    /// <summary>开始时间</summary>
    public DateTimeOffset StartTime { get; private set; }

    /// <summary>结束时间</summary>
    public DateTimeOffset EndTime { get; private set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
