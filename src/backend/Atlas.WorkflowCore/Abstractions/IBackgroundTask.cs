namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 后台任务接口
/// </summary>
public interface IBackgroundTask
{
    /// <summary>
    /// 启动后台任务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task Start(CancellationToken cancellationToken);

    /// <summary>
    /// 停止后台任务
    /// </summary>
    Task Stop();
}
