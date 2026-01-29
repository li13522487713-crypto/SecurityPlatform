namespace Atlas.WorkflowCore.Models;

public enum PointerStatus
{
    Legacy = 0,
    Pending = 1,
    Running = 2,
    Complete = 3,
    Sleeping = 4,
    WaitingForEvent = 5,
    Failed = 6,
    Compensated = 7,
    Cancelled = 8,
    PendingPredecessor = 9
}
