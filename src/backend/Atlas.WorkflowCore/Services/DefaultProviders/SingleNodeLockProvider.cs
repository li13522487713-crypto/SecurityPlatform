using System.Collections.Concurrent;
using Atlas.WorkflowCore.Abstractions;

namespace Atlas.WorkflowCore.Services.DefaultProviders;

/// <summary>
/// 单节点锁提供者 - 使用内存 SemaphoreSlim 实现
/// </summary>
public class SingleNodeLockProvider : IDistributedLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
    private bool _isRunning;

    public SingleNodeLockProvider()
    {
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    public async Task<bool> AcquireLock(string id, CancellationToken cancellationToken)
    {
        if (!_isRunning)
        {
            return false;
        }

        var semaphore = _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

        try
        {
            return await semaphore.WaitAsync(0, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public Task ReleaseLock(string id)
    {
        if (_locks.TryGetValue(id, out var semaphore))
        {
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // 锁已经被释放，忽略
            }
        }

        return Task.CompletedTask;
    }

    public Task Start()
    {
        _isRunning = true;
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        _isRunning = false;

        // 释放所有锁
        foreach (var semaphore in _locks.Values)
        {
            semaphore.Dispose();
        }

        _locks.Clear();

        return Task.CompletedTask;
    }
}
