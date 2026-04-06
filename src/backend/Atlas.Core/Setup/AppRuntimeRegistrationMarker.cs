namespace Atlas.Core.Setup;

/// <summary>
/// 标记 AppHost 启动时是否完整注册了全量业务服务。
/// 当 setup 在运行时完成但 AppHost 未重启时，<see cref="FullyRegistered"/> 为 false，
/// 中间件据此返回 503 APP_RESTART_REQUIRED 而非让请求进入未注册的服务管线。
/// </summary>
public sealed class AppRuntimeRegistrationMarker(bool fullyRegistered)
{
    public bool FullyRegistered { get; } = fullyRegistered;
}
