namespace Atlas.Domain.BatchProcess.Enums;

public enum ShardExecutionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Retrying = 4
}
