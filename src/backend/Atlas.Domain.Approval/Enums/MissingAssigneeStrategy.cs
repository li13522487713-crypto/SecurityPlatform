namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 缺失审批人处理策略
/// </summary>
public enum MissingAssigneeStrategy
{
    /// <summary>不允许发起（0）</summary>
    NotAllowed = 0,

    /// <summary>跳过（1）- 不生成审批任务节点</summary>
    Skip = 1,

    /// <summary>转办给管理员（2）</summary>
    TransferToAdmin = 2
}
