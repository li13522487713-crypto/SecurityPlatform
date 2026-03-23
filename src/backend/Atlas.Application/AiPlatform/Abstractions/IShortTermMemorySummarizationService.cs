using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IShortTermMemorySummarizationService
{
    Task<string?> GetSummaryAsync(
        TenantId tenantId,
        long conversationId,
        CancellationToken cancellationToken);

    Task<string?> TrySummarizeAsync(
        TenantId tenantId,
        long conversationId,
        long agentId,
        long userId,
        IReadOnlyList<ChatHistoryMessage> messages,
        CancellationToken cancellationToken);
}
