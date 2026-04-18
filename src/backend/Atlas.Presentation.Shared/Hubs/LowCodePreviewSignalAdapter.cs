using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Presentation.Shared.Hubs;

/// <summary>
/// 把 ILowCodePreviewSignal（Application 层抽象）适配到 SignalR 实现的
/// ILowCodePreviewBroadcaster；让 Infrastructure 层的写入服务能跨 host 共享同一个推送通道。
/// </summary>
public sealed class LowCodePreviewSignalAdapter : ILowCodePreviewSignal
{
    private readonly ILowCodePreviewBroadcaster _broadcaster;

    public LowCodePreviewSignalAdapter(ILowCodePreviewBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public Task PushSchemaDiffAsync(TenantId tenantId, string appId, object diffPayload, CancellationToken cancellationToken)
        => _broadcaster.PushSchemaDiffAsync(tenantId, appId, diffPayload, cancellationToken);
}
