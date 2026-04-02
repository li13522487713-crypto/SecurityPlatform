namespace Atlas.Domain.BatchProcess.Enums;

public enum DeadLetterStatus
{
    Pending = 0,
    Retrying = 1,
    Resolved = 2,
    Abandoned = 3
}
