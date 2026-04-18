using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Atlas.AppHost.Hubs;

/// <summary>
/// 低代码协同编辑 Hub（M16 S16-1，路径 /hubs/lowcode-collab）。
///
/// - 客户端 JoinApp(appId) 加入 group；
/// - SendUpdate(appId, userId, base64Update)：服务端不解析 Yjs CRDT 内部结构，只做权限校验 + 广播 + 缓存；
/// - ReceiveUpdate 通过 yjsUpdate 事件回推。
///
/// 强约束（PLAN.md §M16）：
/// - 仅承载 Yjs update 二进制中转，不解析 CRDT。
/// - 离线快照由 LowCodeCollabSnapshotJob 周期性落 AppVersionArchive（systemSnapshot=true）。
///
/// P0-6 修复（PLAN §M16 S16-1 + 1.1）：
/// - 此前 [Authorize] + 租户隔离已经满足登录态，但**不校验当前用户是否能访问目标 appId**；
/// - 同租户的任意登录用户可越权 JoinApp("any-app-id") 偷听其他应用的 Yjs 更新；
/// - 现修正为 JoinApp / SendUpdate 时必须校验：appId 必须存在于当前租户的 AppDefinition 中（按 code 或 id 解析）；
/// - 不存在 → HubException("APP_ACCESS_DENIED")；存在则正常入组/广播；
/// - 后续若引入 app 级 ACL（owner/co-editor），可在 IAppDefinitionRepository 上扩展 CanEditAsync 替换本检查。
/// </summary>
[Authorize]
public sealed class LowCodeCollabHub : Hub
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppDefinitionRepository _appRepo;
    private readonly ILogger<LowCodeCollabHub> _logger;

    public LowCodeCollabHub(
        ITenantProvider tenantProvider,
        IAppDefinitionRepository appRepo,
        ILogger<LowCodeCollabHub> logger)
    {
        _tenantProvider = tenantProvider;
        _appRepo = appRepo;
        _logger = logger;
    }

    public async Task JoinApp(string appId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await EnsureCanAccessAppAsync(tenantId, appId);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildGroup(tenantId, appId));
    }

    public Task LeaveApp(string appId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildGroup(tenantId, appId));
    }

    /// <summary>客户端发送 Yjs update（base64）→ 广播给 group 内其它客户端 + 缓存最新 update（M16 S16-2 LowCodeCollabSnapshotJob 消费）。</summary>
    public async Task SendUpdate(string appId, string userId, string base64Update)
    {
        var tenantId = _tenantProvider.GetTenantId();
        // 二次校验：避免通过直接 SendUpdate 绕过 JoinApp
        await EnsureCanAccessAppAsync(tenantId, appId);
        var group = BuildGroup(tenantId, appId);
        // 排除 sender（防止回声）：通过 OthersInGroup
        await Clients.OthersInGroup(group).SendAsync("yjsUpdate", new { from = userId, update = base64Update });
        LowCodeCollabSnapshotCache.Store(tenantId.Value, appId, base64Update);
        _logger.LogTrace("yjsUpdate received tenant={Tenant} app={App} from={User} bytes(approx)={Len}", tenantId.Value, appId, userId, base64Update.Length);
    }

    /// <summary>
    /// P1-6 修复（PLAN §M16 C16-2）：客户端 awareness 协议帧（base64）→ 广播给 group 内其它客户端。
    /// 此前 awareness 仅在前端 Awareness 实例内本地状态，缺跨端传输通道，导致多人光标 / 选区无法互通。
    /// 服务端不解析 awareness 内容（与 yjsUpdate 同样仅做权限校验 + 二进制中转），不入快照（cursor 信息无需持久化）。
    /// </summary>
    public async Task SendAwareness(string appId, string userId, string base64Awareness)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await EnsureCanAccessAppAsync(tenantId, appId);
        var group = BuildGroup(tenantId, appId);
        await Clients.OthersInGroup(group).SendAsync("awareness", new { from = userId, awareness = base64Awareness });
        _logger.LogTrace("awareness received tenant={Tenant} app={App} from={User} bytes(approx)={Len}", tenantId.Value, appId, userId, base64Awareness.Length);
    }

    public static string BuildGroup(TenantId tenantId, string appId) =>
        $"lowcode-collab:{tenantId.Value}:{appId}";

    /// <summary>
    /// app 级权限校验：appId 必须在当前租户内存在；优先按 code 查（前端默认传 code），
    /// fallback 按数字 id 查（兼容某些场景）。
    /// 命中 0 条 → HubException(APP_ACCESS_DENIED)。
    /// </summary>
    private async Task EnsureCanAccessAppAsync(TenantId tenantId, string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new HubException("APP_ACCESS_DENIED: appId 不可为空");
        }

        var byCode = await _appRepo.FindByCodeAsync(tenantId, appId, Context.ConnectionAborted);
        if (byCode is not null) return;

        if (long.TryParse(appId, out var idLong))
        {
            var byId = await _appRepo.FindByIdAsync(tenantId, idLong, Context.ConnectionAborted);
            if (byId is not null) return;
        }

        _logger.LogWarning("LowCodeCollabHub access denied: tenant={Tenant} app={App} connection={Conn}",
            tenantId.Value, appId, Context.ConnectionId);
        throw new HubException($"APP_ACCESS_DENIED: 当前租户内不存在 appId={appId}，禁止协同访问");
    }
}
