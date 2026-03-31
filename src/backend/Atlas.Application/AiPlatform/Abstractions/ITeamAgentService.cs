using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ITeamAgentService
{
    Task<PagedResult<TeamAgentListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<TeamAgentDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        long creatorUserId,
        TeamAgentCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        TeamAgentUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> DuplicateAsync(
        TenantId tenantId,
        long creatorUserId,
        long id,
        CancellationToken cancellationToken);

    Task PublishAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TeamAgentTemplateItem>> GetTemplatesAsync(
        CancellationToken cancellationToken);

    Task<long> CreateFromTemplateAsync(
        TenantId tenantId,
        long creatorUserId,
        TeamAgentCreateFromTemplateRequest request,
        CancellationToken cancellationToken);

    Task<long> CreateConversationAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        TeamAgentConversationCreateRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<TeamAgentConversationDto>> ListConversationsAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<TeamAgentConversationDto?> GetConversationAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        CancellationToken cancellationToken);

    Task UpdateConversationAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        TeamAgentConversationUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteConversationAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        CancellationToken cancellationToken);

    Task ClearConversationContextAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        CancellationToken cancellationToken);

    Task ClearConversationHistoryAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TeamAgentMessageDto>> GetConversationMessagesAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        bool includeContextMarkers,
        int? limit,
        CancellationToken cancellationToken);

    Task<TeamAgentChatResponse> ChatAsync(
        TenantId tenantId,
        long userId,
        long teamAgentId,
        TeamAgentChatRequest request,
        string? appId,
        CancellationToken cancellationToken);

    IAsyncEnumerable<TeamAgentRunEvent> ChatStreamAsync(
        TenantId tenantId,
        long userId,
        long teamAgentId,
        TeamAgentChatRequest request,
        string? appId,
        CancellationToken cancellationToken);

    Task CancelChatAsync(
        TenantId tenantId,
        long userId,
        long teamAgentId,
        TeamAgentChatCancelRequest request,
        CancellationToken cancellationToken);

    Task<TeamAgentExecutionResult?> GetExecutionAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken);

    Task<long> CreateSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        SchemaDraftCreateRequest request,
        string? appId,
        CancellationToken cancellationToken);

    Task<TeamAgentSchemaDraftDetail?> GetSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        CancellationToken cancellationToken);

    Task UpdateSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        long userId,
        SchemaDraftUpdateRequest request,
        CancellationToken cancellationToken);

    Task<SchemaDraftConfirmationResponse> ConfirmSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        long userId,
        SchemaDraftConfirmationRequest request,
        CancellationToken cancellationToken);

    Task DiscardSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        CancellationToken cancellationToken);
}
