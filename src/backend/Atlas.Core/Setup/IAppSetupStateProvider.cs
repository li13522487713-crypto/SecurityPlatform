namespace Atlas.Core.Setup;

/// <summary>
/// 应用级安装状态提供器，独立于平台级 setup state。
/// </summary>
public interface IAppSetupStateProvider
{
    bool IsReady { get; }

    AppSetupStateInfo GetState();

    Task TransitionAsync(AppSetupState target, string? failureMessage = null, CancellationToken cancellationToken = default);

    Task CompleteSetupAsync(string appName, string adminUsername, string? appKey = null, CancellationToken cancellationToken = default);
}
