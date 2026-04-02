using Atlas.Application.BatchProcess.Abstractions;

namespace Atlas.Infrastructure.BatchProcess.Scheduling;

/// <summary>
/// 信号量工作池：控制并发数量，支持动态调整并发度和背压信号。
/// </summary>
public sealed class WorkerPool : IWorkerPool, IDisposable
{
    private readonly Lock _sync = new();
    private int _maxConcurrency;
    private int _activeWorkers;

    public WorkerPool(int maxConcurrency)
    {
        _maxConcurrency = maxConcurrency;
    }

    public int MaxConcurrency => _maxConcurrency;
    public int ActiveWorkers => _activeWorkers;
    public bool IsUnderPressure => _activeWorkers >= _maxConcurrency;

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(100);
        while (DateTime.UtcNow < deadline)
        {
            lock (_sync)
            {
                if (_activeWorkers < _maxConcurrency)
                {
                    _activeWorkers++;
                    return true;
                }
            }

            await Task.Delay(10, cancellationToken);
        }

        return false;
    }

    public void Release()
    {
        lock (_sync)
        {
            if (_activeWorkers > 0)
            {
                _activeWorkers--;
            }
        }
    }

    public void AdjustConcurrency(int newMax)
    {
        if (newMax < 1) newMax = 1;
        lock (_sync)
        {
            _maxConcurrency = newMax;
        }
    }

    public void Dispose()
    {
        // no-op
    }
}
