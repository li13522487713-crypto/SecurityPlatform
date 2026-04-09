namespace Atlas.Application.AiPlatform.Models;

public enum NodeLifecycleState
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    TimedOut = 4,
    Compensated = 5
}
