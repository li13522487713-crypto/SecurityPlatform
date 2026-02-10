using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// AI 审批处理器接口
/// </summary>
public interface IApprovalAiHandler
{
    /// <summary>
    /// AI 智能审批（返回 true 表示通过，false 表示驳回/转人工）
    /// </summary>
    Task<AiApprovalResult> HandleAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        AiNodeContext node,
        CancellationToken cancellationToken);

    /// <summary>
    /// AI 路由决策（返回目标节点ID）
    /// </summary>
    Task<string?> DecideRouteAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        AiNodeContext node,
        CancellationToken cancellationToken);
}

public sealed class AiNodeContext
{
    public string NodeId { get; init; } = string.Empty;
    public string? NodeName { get; init; }
    public string NodeType { get; init; } = string.Empty;
    public string? AiConfig { get; init; }
    public string? TriggerType { get; init; }
}

public class AiApprovalResult
{
    public bool Approved { get; set; }
    public string? Comment { get; set; }
    public bool NeedManualReview { get; set; } // 是否需要转人工
}
