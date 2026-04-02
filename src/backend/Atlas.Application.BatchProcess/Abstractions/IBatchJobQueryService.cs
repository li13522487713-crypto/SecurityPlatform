using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Abstractions;

public interface IBatchJobQueryService
{
    Task<PagedResult<BatchJobDefinitionListItem>> QueryJobsAsync(
        PagedRequest request,
        BatchJobStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<BatchJobDefinitionResponse?> GetJobByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);

    Task<PagedResult<BatchJobExecutionListItem>> QueryExecutionsAsync(
        long jobDefinitionId,
        PagedRequest request,
        JobExecutionStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<BatchJobExecutionResponse?> GetExecutionByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShardExecutionResponse>> GetShardsByExecutionIdAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken);
}
