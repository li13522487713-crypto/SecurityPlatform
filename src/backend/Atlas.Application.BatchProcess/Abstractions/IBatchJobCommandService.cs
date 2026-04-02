using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.BatchProcess.Abstractions;

public interface IBatchJobCommandService
{
    Task<long> CreateAsync(BatchJobCreateRequest request, TenantId tenantId, string createdBy, CancellationToken cancellationToken);
    Task UpdateAsync(long id, BatchJobUpdateRequest request, TenantId tenantId, CancellationToken cancellationToken);
    Task ActivateAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task PauseAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task ArchiveAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<long> TriggerAsync(long id, TenantId tenantId, string triggeredBy, CancellationToken cancellationToken);
    Task CancelExecutionAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken);
}
