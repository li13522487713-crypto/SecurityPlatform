using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiSearchService
{
    Task<AiSearchResponse> SearchAsync(
        TenantId tenantId,
        long userId,
        string? keyword,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiRecentEditItem>> GetRecentEditsAsync(
        TenantId tenantId,
        long userId,
        int limit,
        CancellationToken cancellationToken);

    Task<long> RecordRecentEditAsync(
        TenantId tenantId,
        long userId,
        AiRecentEditCreateRequest request,
        CancellationToken cancellationToken);

    Task DeleteRecentEditAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken);
}
