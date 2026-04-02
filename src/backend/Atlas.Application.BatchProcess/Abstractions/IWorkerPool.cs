namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// 工作池：控制分片/批次的并发执行数量，支持背压（backpressure）信号。
/// </summary>
public interface IWorkerPool
{
    int MaxConcurrency { get; }
    int ActiveWorkers { get; }
    bool IsUnderPressure { get; }

    Task<bool> TryAcquireAsync(CancellationToken cancellationToken);
    void Release();
    void AdjustConcurrency(int newMax);
}
