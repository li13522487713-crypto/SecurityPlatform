using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ILongTermMemoryExtractionService
{
    Task<IReadOnlyList<LongTermMemoryRecallItem>> RecallAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        string query,
        int? topK,
        CancellationToken cancellationToken);

    Task ExtractAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken);
}
