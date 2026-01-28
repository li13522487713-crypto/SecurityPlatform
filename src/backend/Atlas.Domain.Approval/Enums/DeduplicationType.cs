namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 去重类型
/// </summary>
public enum DeduplicationType
{
    /// <summary>不去重（0）</summary>
    None = 0,

    /// <summary>前向去重（1）- 排除已在之前节点审批过的用户</summary>
    Forward = 1,

    /// <summary>后向去重（2）- 排除已在之后节点审批过的用户</summary>
    Backward = 2,

    /// <summary>双向去重（3）- 前向和后向都去重</summary>
    Both = 3
}
