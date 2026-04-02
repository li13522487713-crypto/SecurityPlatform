using Atlas.Core.Resilience;

namespace Atlas.Infrastructure.Resilience;

public sealed class SimpleCircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private int _failureCount;
    private long _openUntilTicks;
    private int _state;
    private bool _halfOpenBusy;
    private readonly object _sync = new();

    public SimpleCircuitBreaker(int failureThreshold = 5, TimeSpan? openDuration = null)
    {
        _failureThreshold = failureThreshold < 1 ? 1 : failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(30);
    }

    public CircuitState State
    {
        get
        {
            lock (_sync)
            {
                TransitionIfDue();
                return MapState(_state);
            }
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        EnterExecute();
        try
        {
            var result = await action(ct).ConfigureAwait(false);
            _ = Interlocked.Exchange(ref _failureCount, 0);
            lock (_sync)
            {
                _state = 0;
                _halfOpenBusy = false;
            }

            return result;
        }
        catch (Exception)
        {
            OnExecuteFailed();
            throw;
        }
    }

    public void RecordSuccess()
    {
        _ = Interlocked.Exchange(ref _failureCount, 0);
        lock (_sync)
        {
            _state = 0;
            _halfOpenBusy = false;
        }
    }

    public void RecordFailure()
    {
        var c = Interlocked.Increment(ref _failureCount);
        lock (_sync)
        {
            _halfOpenBusy = false;
            if (c >= _failureThreshold)
            {
                _state = 1;
                _openUntilTicks = DateTime.UtcNow.Add(_openDuration).Ticks;
            }
        }
    }

    private void EnterExecute()
    {
        lock (_sync)
        {
            TransitionIfDue();
            if (_state == 1)
                throw new InvalidOperationException("Circuit breaker is open.");

            if (_state == 2)
            {
                if (_halfOpenBusy)
                    throw new InvalidOperationException("Circuit breaker half-open trial already in progress.");
                _halfOpenBusy = true;
            }
        }
    }

    private void OnExecuteFailed()
    {
        var c = Interlocked.Increment(ref _failureCount);
        lock (_sync)
        {
            _halfOpenBusy = false;
            if (_state == 2 || c >= _failureThreshold)
            {
                _state = 1;
                _openUntilTicks = DateTime.UtcNow.Add(_openDuration).Ticks;
            }
        }
    }

    private void TransitionIfDue()
    {
        if (_state == 1 && DateTime.UtcNow.Ticks >= Volatile.Read(ref _openUntilTicks))
        {
            _state = 2;
            _ = Interlocked.Exchange(ref _failureCount, 0);
        }
    }

    private static CircuitState MapState(int state) =>
        state switch
        {
            1 => CircuitState.Open,
            2 => CircuitState.HalfOpen,
            _ => CircuitState.Closed
        };
}
