namespace Atlas.Application.Microflows.Infrastructure;

public interface IMicroflowClock
{
    DateTimeOffset UtcNow { get; }
}
