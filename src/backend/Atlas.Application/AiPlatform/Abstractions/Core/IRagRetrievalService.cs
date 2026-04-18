using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IRagRetrievalService
{
    Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 5,
        RagRetrievalFilter? filter = null,
        CancellationToken ct = default);
}
