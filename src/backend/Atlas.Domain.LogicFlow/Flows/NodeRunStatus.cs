namespace Atlas.Domain.LogicFlow.Flows;

public enum NodeRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4,
    TimedOut = 5,
    Compensating = 6,
    Compensated = 7,
    WaitingForRetry = 8,
}
