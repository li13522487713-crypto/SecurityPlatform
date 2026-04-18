using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Atlas.Presentation.Shared.Hubs;

/// <summary>
/// Preview HMR Hub（M08 S08-3，路径 <c>/hubs/lowcode-preview</c>）。
///
/// - 客户端通过 JoinApp 加入 appId connection group。
/// - 设计态 autosave / replaceDraft 触发时，PlatformHost 后端 → 推送 schemaDiff 到该 group。
/// - 仅承载传输信令；具体 schema diff 计算由调用方完成。
///
/// 强约束（PLAN.md §M08 C08-9）：
/// - 调试预览壳（lowcode-preview-web 5184）通过本 Hub 实现毫秒级热更新，不需要重新发布。
/// - Hub 同时挂载在 PlatformHost / AppHost：PlatformHost 触发推送，AppHost 与 PlatformHost 共享 SignalR backplane（默认内存，多实例需要 Redis backplane）。
/// </summary>
[Authorize]
public sealed class LowCodePreviewHub : Hub
{
    private readonly ITenantProvider _tenantProvider;

    public LowCodePreviewHub(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public Task JoinApp(string appId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        return Groups.AddToGroupAsync(Context.ConnectionId, BuildGroup(tenantId, appId));
    }

    public Task LeaveApp(string appId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildGroup(tenantId, appId));
    }

    public static string BuildGroup(TenantId tenantId, string appId) =>
        $"lowcode-preview:{tenantId.Value}:{appId}";
}

/// <summary>
/// Preview Hub 推送服务：供设计态 autosave / replaceDraft / snapshot 完成时调用。
/// </summary>
public interface ILowCodePreviewBroadcaster
{
    Task PushSchemaDiffAsync(TenantId tenantId, string appId, object diffPayload, CancellationToken cancellationToken);
}

public sealed class LowCodePreviewBroadcaster : ILowCodePreviewBroadcaster
{
    private readonly IHubContext<LowCodePreviewHub> _hub;

    public LowCodePreviewBroadcaster(IHubContext<LowCodePreviewHub> hub)
    {
        _hub = hub;
    }

    public Task PushSchemaDiffAsync(TenantId tenantId, string appId, object diffPayload, CancellationToken cancellationToken)
    {
        return _hub.Clients.Group(LowCodePreviewHub.BuildGroup(tenantId, appId))
            .SendAsync("schemaDiff", diffPayload, cancellationToken);
    }
}

/// <summary>
/// 当 host 没有挂载 LowCodePreviewHub 时使用的 NoOp 实现（避免 ResolveService 失败）。
/// 实际生产部署中两个 host 都挂载 Hub 后即默认用 LowCodePreviewBroadcaster。
/// </summary>
public sealed class NoOpLowCodePreviewBroadcaster : ILowCodePreviewBroadcaster
{
    public Task PushSchemaDiffAsync(TenantId tenantId, string appId, object diffPayload, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
