using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IConversationService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        ConversationCreateRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<ConversationDto>> ListByAgentAsync(
        TenantId tenantId,
        long agentId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResult<ConversationDto>> ListByUserAsync(
        TenantId tenantId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ConversationDto?> GetByIdAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        ConversationUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken);

    Task ClearHistoryAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken);

    Task ClearContextAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        bool includeContextMarkers,
        int? limit,
        CancellationToken cancellationToken);

    Task DeleteMessageAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        long messageId,
        CancellationToken cancellationToken);
}
