namespace Atlas.Core.Setup;

/// <summary>
/// 安装状态提供器：在整个宿主生命周期中提供 setup 状态的查询与转换。
/// 实现不依赖数据库，使用文件持久化。
/// </summary>
public interface ISetupStateProvider
{
    /// <summary>当前是否已完成安装（Ready 状态）。</summary>
    bool IsReady { get; }

    /// <summary>获取当前安装状态快照。</summary>
    SetupStateInfo GetState();

    /// <summary>将状态转换到指定阶段并持久化。</summary>
    Task TransitionAsync(SetupState target, string? failureMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在 setup 完成时设置数据库信息并转到 Ready。
    /// </summary>
    Task CompleteSetupAsync(SetupDatabaseInfo databaseInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 阻塞等待直到进入 Ready 状态或取消。
    /// 供 gated HostedService 使用。
    /// </summary>
    Task WaitForReadyAsync(CancellationToken cancellationToken);
}
