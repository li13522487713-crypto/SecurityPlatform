using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface IRuntimeTraceRepository
{
    Task<long> InsertTraceAsync(RuntimeTrace trace, CancellationToken cancellationToken);
    Task<bool> UpdateTraceAsync(RuntimeTrace trace, CancellationToken cancellationToken);
    Task<int> InsertSpansBatchAsync(IReadOnlyList<RuntimeSpan> spans, CancellationToken cancellationToken);
    Task<RuntimeTrace?> FindByTraceIdAsync(TenantId tenantId, string traceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RuntimeSpan>> ListSpansByTraceAsync(TenantId tenantId, string traceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RuntimeTrace>> QueryTracesAsync(TenantId tenantId, string? appId, string? pageId, string? componentId, DateTimeOffset? from, DateTimeOffset? to, string? errorKind, long? userId, int pageIndex, int pageSize, CancellationToken cancellationToken);
}
