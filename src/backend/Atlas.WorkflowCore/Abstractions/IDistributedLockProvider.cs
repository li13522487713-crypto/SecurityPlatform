namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 分布式锁提供者接口
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>
    /// 获取指定资源的锁
    /// </summary>
    /// <param name="id">资源ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果成功获取锁返回true，否则返回false</returns>
    Task<bool> AcquireLock(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 释放指定资源的锁
    /// </summary>
    /// <param name="id">资源ID</param>
    Task ReleaseLock(string id);

    /// <summary>
    /// 启动锁提供者
    /// </summary>
    Task Start();

    /// <summary>
    /// 停止锁提供者
    /// </summary>
    Task Stop();
}
