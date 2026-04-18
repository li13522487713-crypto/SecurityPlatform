using System.Collections.Concurrent;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Atlas.AppHost.Hubs;

/// <summary>
/// 低代码协同编辑 Hub（M16 S16-1，路径 /hubs/lowcode-collab）。
///
/// - 客户端 JoinApp(appId) 加入 group；
/// - SendUpdate(appId, userId, base64Update)：服务端不解析 Yjs CRDT 内部结构，只做权限校验 + 广播 + 落库；
/// - ReceiveUpdate 通过 yjsUpdate 事件回推。
///
/// 强约束（PLAN.md §M16）：
/// - 仅承载 Yjs update 二进制中转，不解析 CRDT。
/// - 离线快照由 LowCodeCollabSnapshotJob 周期性落 AppVersionArchive（systemSnapshot=true）。
/// </summary>
[Authorize]
public sealed class LowCodeCollabHub : Hub
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<LowCodeCollabHub> _logger;

    public LowCodeCollabHub(ITenantProvider tenantProvider, ILogger<LowCodeCollabHub> logger)
    {
        _tenantProvider = tenantProvider;
        _logger = logger;
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

    /// <summary>客户端发送 Yjs update（base64）→ 广播给 group 内其它客户端 + 缓存最新 update（M16 后续接入快照存储）。</summary>
    public async Task SendUpdate(string appId, string userId, string base64Update)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var group = BuildGroup(tenantId, appId);
        // 排除 sender（防止回声）：通过 OthersInGroup
        await Clients.OthersInGroup(group).SendAsync("yjsUpdate", new { from = userId, update = base64Update });
        // M16 阶段把最近一次 update 暂存内存；下一阶段持久化到 AppVersionArchive 由 LowCodeCollabSnapshotJob 完成。
        LowCodeCollabSnapshotCache.Store(tenantId.Value, appId, base64Update);
        _logger.LogTrace("yjsUpdate received tenant={Tenant} app={App} from={User} bytes(approx)={Len}", tenantId.Value, appId, userId, base64Update.Length);
    }

    public static string BuildGroup(TenantId tenantId, string appId) =>
        $"lowcode-collab:{tenantId.Value}:{appId}";
}

/// <summary>
/// 协同最新 update 的内存缓存（M16 阶段简化）。
/// 真正的离线快照持久化由 LowCodeCollabSnapshotJob（周期 10 分钟）落 AppVersionArchive。
/// </summary>
internal static class LowCodeCollabSnapshotCache
{
    private static readonly ConcurrentDictionary<string, string> Latest = new();

    private static string Key(Guid tenantId, string appId) => $"{tenantId}:{appId}";

    public static void Store(Guid tenantId, string appId, string base64Update)
    {
        Latest[Key(tenantId, appId)] = base64Update;
    }

    public static string? TryGet(Guid tenantId, string appId)
    {
        return Latest.TryGetValue(Key(tenantId, appId), out var v) ? v : null;
    }

    public static IReadOnlyDictionary<string, string> Snapshot()
    {
        return new Dictionary<string, string>(Latest);
    }
}
