namespace Atlas.Application.Microflows.Infrastructure;

public sealed class SystemMicroflowClock : IMicroflowClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
