namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 节点运行时 13 态状态机。
/// </summary>
public enum NodeExecutionStatus
{
    Pending = 0,
    Ready = 1,
    Running = 2,
    Paused = 3,
    Completed = 4,
    Failed = 5,
    Skipped = 6,
    Cancelled = 7,
    TimedOut = 8,
    WaitingForRetry = 9,
    Compensating = 10,
    Compensated = 11,
    Unknown = 12,
}
