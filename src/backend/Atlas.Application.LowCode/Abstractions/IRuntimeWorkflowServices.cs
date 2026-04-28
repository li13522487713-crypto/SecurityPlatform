using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 运行时工作流执行服务（M09 S09-1）。
///
/// 内部桥接 Coze workflow 执行服务，
/// 增强：
///  - 弹性策略（超时 / 重试 / 熔断 / 降级）
///  - 异步任务持久化与查询
///  - 批量执行（Hangfire 调度，阶段性产出 RuntimeWorkflowBatchResult）
/// </summary>
public interface IRuntimeWorkflowExecutor
{
    Task<RuntimeWorkflowInvokeResult> InvokeAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowInvokeRequest request, CancellationToken cancellationToken);

    Task<string> SubmitAsyncAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowInvokeRequest request, CancellationToken cancellationToken);

    Task<RuntimeWorkflowAsyncJobDto?> GetAsyncJobAsync(TenantId tenantId, string jobId, CancellationToken cancellationToken);

    Task CancelAsyncJobAsync(TenantId tenantId, long currentUserId, string jobId, CancellationToken cancellationToken);

    Task<RuntimeWorkflowBatchResult> InvokeBatchAsync(TenantId tenantId, long currentUserId, RuntimeWorkflowBatchInvokeRequest request, CancellationToken cancellationToken);
}
