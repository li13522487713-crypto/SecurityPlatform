namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批紧急程度
/// </summary>
public enum ApprovalUrgencyLevel
{
    /// <summary>普通</summary>
    Normal = 0,

    /// <summary>加急</summary>
    Urgent = 1,

    /// <summary>特急</summary>
    VeryUrgent = 2
}
