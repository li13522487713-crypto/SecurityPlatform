namespace Atlas.Core.Setup;

/// <summary>
/// 安装状态提供器：在整个宿主生命周期中提供 setup 状态的查询与转换。
/// 实现不依赖数据库，使用文件持久化。
/// </summary>
public interface ISetupStateProvider
{
    /// <summary>当前是否已完成安装（Ready 状态）。</summary>
    bool IsReady { get; }

    /// <summary>
    /// 当前是否正在执行安装过程（Configuring/Migrating/Seeding 状态）。
    /// 供 ISqlSugarClient 工厂在 setup 期间豁免门禁使用。
    /// </summary>
    bool IsSetupInProgress { get; }

    /// <summary>获取当前安装状态快照。</summary>
    SetupStateInfo GetState();

    /// <summary>将状态转换到指定阶段并持久化。</summary>
    Task TransitionAsync(SetupState target, string? failureMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记安装完成并转到 Ready 状态。
    /// 数据库配置已持久化至 appsettings.runtime.json，不再通过此方法传递。
    /// </summary>
    Task CompleteSetupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 阻塞等待直到进入 Ready 状态或取消。
    /// 供 gated HostedService 使用。
    /// </summary>
    Task WaitForReadyAsync(CancellationToken cancellationToken);
}
