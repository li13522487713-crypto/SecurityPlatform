namespace Atlas.Domain.LogicFlow.Flows;

public enum ExecutionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    TimedOut = 5,
    Paused = 6,
    Compensating = 7,
    Compensated = 8,
}
