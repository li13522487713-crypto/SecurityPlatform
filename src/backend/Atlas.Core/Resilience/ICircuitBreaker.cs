namespace Atlas.Core.Resilience;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public interface ICircuitBreaker
{
    CircuitState State { get; }

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);

    void RecordSuccess();

    void RecordFailure();
}
