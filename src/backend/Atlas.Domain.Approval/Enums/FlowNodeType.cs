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
    ExternalCondition = 7,

    /// <summary>包容网关（Inclusive Gateway）- 条件+并行结合体</summary>
    InclusiveGateway = 8,

    /// <summary>路由网关（Route Gateway）- 重定向到指定节点</summary>
    RouteGateway = 9,

    /// <summary>子流程节点</summary>
    CallProcess = 10,

    /// <summary>定时器节点</summary>
    Timer = 11,

    /// <summary>触发器节点</summary>
    Trigger = 12
}
