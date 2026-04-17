using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface ILowCodeSessionRepository
{
    Task<long> InsertAsync(LowCodeSession session, CancellationToken cancellationToken);
    Task<LowCodeSession?> FindBySessionIdAsync(TenantId tenantId, string sessionId, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(LowCodeSession session, CancellationToken cancellationToken);
    Task<IReadOnlyList<LowCodeSession>> ListByUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task<bool> ClearMessagesAsync(TenantId tenantId, string sessionId, CancellationToken cancellationToken);
}

public interface ILowCodeMessageLogRepository
{
    Task<int> InsertAsync(LowCodeMessageLogEntry entry, CancellationToken cancellationToken);
    Task<int> InsertBatchAsync(IReadOnlyList<LowCodeMessageLogEntry> entries, CancellationToken cancellationToken);
    Task<IReadOnlyList<LowCodeMessageLogEntry>> QueryAsync(TenantId tenantId, string? sessionId, string? workflowId, string? agentId, DateTimeOffset? from, DateTimeOffset? to, int pageIndex, int pageSize, CancellationToken cancellationToken);
}
