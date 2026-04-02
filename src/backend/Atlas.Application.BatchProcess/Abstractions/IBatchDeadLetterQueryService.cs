using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Abstractions;

public interface IBatchDeadLetterQueryService
{
    Task<PagedResult<BatchDeadLetterListItem>> QueryAsync(
        long? jobExecutionId,
        DeadLetterStatus? status,
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<BatchDeadLetterResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}
