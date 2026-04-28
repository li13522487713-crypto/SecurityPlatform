using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IRuntimeSessionService
{
    Task<IReadOnlyList<RuntimeSessionInfo>> ListAsync(TenantId tenantId, long currentUserId, CancellationToken cancellationToken);
    Task<string> CreateAsync(TenantId tenantId, long currentUserId, RuntimeSessionCreateRequest request, CancellationToken cancellationToken);
    Task ClearAsync(TenantId tenantId, long currentUserId, string sessionId, CancellationToken cancellationToken);
    Task PinAsync(TenantId tenantId, long currentUserId, string sessionId, RuntimeSessionPinRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(TenantId tenantId, long currentUserId, string sessionId, RuntimeSessionArchiveRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 多会话切换（M11 C11-6 + S11-2，P0 修复 HttpSessionAdapter 与后端契约断裂）。
    ///
    /// 语义：
    ///  - 校验 sessionId 存在且属当前用户/租户；
    ///  - Touch 该会话（更新 UpdatedAt 作为"最近活跃"），后续 chatflow.invoke 与 message-log 都按 sessionId 路由；
    ///  - 服务端不持久化"当前活跃 sessionId"——前端将返回的 sessionId 作为后续请求 SessionId 参数；
    ///  - 写审计 lowcode.runtime.session.switch。
    ///
    /// 不存在 → BusinessException("NotFound") → 404；跨用户 → BusinessException("Forbidden") → 403。
    /// </summary>
    Task<RuntimeSessionInfo> SwitchAsync(TenantId tenantId, long currentUserId, string sessionId, CancellationToken cancellationToken);
}

public interface IRuntimeChatflowService
{
    /// <summary>
    /// 流式调用：返回的事件流由调用方写入 SSE Response。
    /// 每帧 ChatChunk 序列化为 `data: {json}\n\n`。
    /// </summary>
    IAsyncEnumerable<string> StreamSseAsync(TenantId tenantId, long currentUserId, RuntimeChatflowInvokeRequest request, CancellationToken cancellationToken);

    Task PauseAsync(TenantId tenantId, long currentUserId, string sessionId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResumeSseAsync(TenantId tenantId, long currentUserId, string sessionId, CancellationToken cancellationToken);
    Task InjectAsync(TenantId tenantId, long currentUserId, string sessionId, RuntimeChatflowInjectRequest request, CancellationToken cancellationToken);
}

public interface IRuntimeMessageLogService
{
    Task<IReadOnlyList<RuntimeMessageLogEntryDto>> QueryAsync(TenantId tenantId, RuntimeMessageLogQuery query, CancellationToken cancellationToken);
    /// <summary>由 chatflow / workflow / agent / tool / dispatch 各域调用：写入统一时间线。</summary>
    Task RecordAsync(TenantId tenantId, string source, string kind, string? sessionId, string? workflowId, string? agentId, string? traceId, string payloadJson, CancellationToken cancellationToken);
}
