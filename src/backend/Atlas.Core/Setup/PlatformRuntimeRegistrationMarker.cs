namespace Atlas.Core.Setup;

/// <summary>
/// 标记 PlatformHost 启动时是否完整注册了全量业务服务。
/// 当平台 setup 在运行时完成但 PlatformHost 未重启时，<see cref="FullyRegistered"/> 为 false，
/// 中间件据此返回 503 PLATFORM_RESTART_REQUIRED，避免请求进入未注册完整的运行时管线。
/// </summary>
public sealed class PlatformRuntimeRegistrationMarker(bool fullyRegistered)
{
    public bool FullyRegistered { get; } = fullyRegistered;
}
