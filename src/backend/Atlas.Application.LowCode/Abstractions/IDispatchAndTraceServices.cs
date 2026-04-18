using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// dispatch 控制器使用的统一执行入口（M13 S13-1）。
/// 聚合 RuntimeWorkflowExecutor / Chatflow / Trigger / Asset / Plugin / WebviewPolicy 各域；
/// 同时把 spans 写入 RuntimeTraceService。
/// </summary>
public interface IDispatchExecutor
{
    Task<DispatchResponse> DispatchAsync(TenantId tenantId, long currentUserId, DispatchRequest request, CancellationToken cancellationToken);
}

public interface IRuntimeTraceService
{
    Task<RuntimeTraceDto?> GetTraceAsync(TenantId tenantId, string traceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RuntimeTraceDto>> QueryAsync(TenantId tenantId, RuntimeTraceQuery query, CancellationToken cancellationToken);

    /// <summary>开始一段 trace；返回 traceId。</summary>
    Task<string> StartTraceAsync(TenantId tenantId, long currentUserId, string appId, string? pageId, string? componentId, string? eventName, CancellationToken cancellationToken);
    Task FinishTraceAsync(TenantId tenantId, string traceId, bool success, string? errorKind, CancellationToken cancellationToken);
    Task<string> AddSpanAsync(TenantId tenantId, string traceId, string? parentSpanId, string name, string? attributesJson, bool ok, string? errorMessage, CancellationToken cancellationToken);
}

/// <summary>
/// 简易脱敏中间件（M13 S13-4）。可在 RuntimeTraceService.AddSpanAsync 写入前调用。
/// </summary>
public interface ISensitiveMaskingService
{
    string Mask(string? input);
}
