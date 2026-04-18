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
