using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// Preview HMR 推送信号抽象（M08 S08-3）。
///
/// Application / Infrastructure 层依赖此接口；具体 SignalR 实现位于
/// <c>Atlas.Presentation.Shared.Hubs.LowCodePreviewBroadcaster</c>，由 PlatformHost / AppHost
/// 在启动时通过 adapter 注册到本接口。
///
/// 设计目的：让 AppDefinitionCommandService 等设计态写入服务能在 ReplaceDraft / AutoSave
/// 完成后通知所有订阅 lowcode-preview hub 的客户端，实现毫秒级 HMR。
/// </summary>
public interface ILowCodePreviewSignal
{
    Task PushSchemaDiffAsync(TenantId tenantId, string appId, object diffPayload, CancellationToken cancellationToken);
}

/// <summary>
/// 当 host 不支持 SignalR 推送（例如 unit test 场景）或 hub 未挂载时使用的 NoOp 实现。
/// </summary>
public sealed class NoOpLowCodePreviewSignal : ILowCodePreviewSignal
{
    public Task PushSchemaDiffAsync(TenantId tenantId, string appId, object diffPayload, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
