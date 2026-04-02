using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// 批次重试服务：对失败的批次执行重试策略。
/// </summary>
public interface IBatchRetryService
{
    Task<RetryResult> RetryFailedShardsAsync(long jobExecutionId, TenantId tenantId, CancellationToken cancellationToken);
}
