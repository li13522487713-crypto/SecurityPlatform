using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IMemoryProvider
{
    Task UpsertAsync(
        TenantId tenantId,
        MemoryNamespace memoryNamespace,
        long userId,
        long agentId,
        IReadOnlyCollection<MemoryRecordInput> records,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRecord>> GetRecentAsync(
        TenantId tenantId,
        MemoryNamespace memoryNamespace,
        long userId,
        long agentId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRecord>> GetByKeysAsync(
        TenantId tenantId,
        MemoryNamespace memoryNamespace,
        long userId,
        long agentId,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default);
}
