namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 流程节点类型（对齐 AntFlow.net 的节点类型）
/// </summary>
public enum FlowNodeType
{
    /// <summary>开始节点</summary>
    Start = 0,

    /// <summary>审批节点</summary>
    Approve = 1,

    /// <summary>条件节点（内部条件）</summary>
    Condition = 2,

    /// <summary>结束节点</summary>
    End = 3,

    /// <summary>排他网关（Exclusive Gateway，XOR）- 条件分支，只走一条路径</summary>
    ExclusiveGateway = 4,

    /// <summary>并行网关（Parallel Gateway，AND）- 并行分支，所有路径都要走</summary>
    ParallelGateway = 5,

    /// <summary>抄送节点</summary>
    Copy = 6,

    /// <summary>接入方条件节点（外部条件）</summary>
    ExternalCondition = 7
}
