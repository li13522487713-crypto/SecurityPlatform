using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 队列提供者接口 - 管理分布式工作队列
/// </summary>
public interface IQueueProvider
{
    /// <summary>
    /// 将工作项入队
    /// </summary>
    /// <param name="id">工作项ID</param>
    /// <param name="queue">队列类型</param>
    Task QueueWork(string id, QueueType queue);

    /// <summary>
    /// 从队列中出队工作项
    /// </summary>
    /// <param name="queue">队列类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作项ID，如果队列为空返回null</returns>
    Task<string?> DequeueWork(QueueType queue, CancellationToken cancellationToken);

    /// <summary>
    /// 出队操作是否阻塞（如果为true，DequeueWork会阻塞直到有工作项）
    /// </summary>
    bool IsDequeueBlocking { get; }

    /// <summary>
    /// 启动队列提供者
    /// </summary>
    Task Start();

    /// <summary>
    /// 停止队列提供者
    /// </summary>
    Task Stop();
}
