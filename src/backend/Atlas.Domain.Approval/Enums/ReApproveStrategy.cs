namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 重新审批策略（驳回后）
/// </summary>
public enum ReApproveStrategy
{
    /// <summary>从驳回节点继续往后执行</summary>
    Continue = 1,

    /// <summary>重新从驳回目标节点开始审批</summary>
    BackToRejectNode = 2
}
