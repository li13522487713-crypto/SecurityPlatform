namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批模式（会签/或签）
/// </summary>
public enum ApprovalMode
{
    /// <summary>会签（全部通过）</summary>
    All = 0,

    /// <summary>或签（任一通过）</summary>
    Any = 1,

    /// <summary>顺序会签（依次审批）</summary>
    Sequential = 2,

    /// <summary>票签（按权重投票）</summary>
    Vote = 3
}
