using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Core.Utilities;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using ChatMessageEntity = Atlas.Domain.AiPlatform.Entities.ChatMessage;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ConversationService : IConversationService
{
    private readonly ConversationRepository _conversationRepository;
    private readonly ChatMessageRepository _chatMessageRepository;
    private readonly AgentRepository _agentRepository;
    private readonly IConversationOwnerResolver _conversationOwnerResolver;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public ConversationService(
        ConversationRepository conversationRepository,
        ChatMessageRepository chatMessageRepository,
        AgentRepository agentRepository,
        IConversationOwnerResolver conversationOwnerResolver,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _chatMessageRepository = chatMessageRepository;
        _agentRepository = agentRepository;
        _conversationOwnerResolver = conversationOwnerResolver;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        ConversationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.FindByIdAsync(tenantId, request.AgentId, cancellationToken);
        if (agent is null)
        {
            throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? $"与 {agent.Name} 的会话"
            : request.Title.Trim();
        var effectiveUserId = await _conversationOwnerResolver.ResolveAsync(tenantId, userId, cancellationToken);
        var entity = new Conversation(
            tenantId,
            request.AgentId,
            effectiveUserId,
            title,
            _idGeneratorAccessor.NextId());

        await _conversationRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task<PagedResult<ConversationDto>> ListByAgentAsync(
        TenantId tenantId,
        long agentId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var effectiveUserId = await _conversationOwnerResolver.ResolveAsync(tenantId, userId, cancellationToken);
        var (items, total) = await _conversationRepository.GetPagedByAgentAsync(
            tenantId,
            agentId,
            effectiveUserId,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<ConversationDto>(items.Select(MapConversation).ToList(), total, pageIndex, pageSize);
    }

    public async Task<PagedResult<ConversationDto>> ListByUserAsync(
        TenantId tenantId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var effectiveUserId = await _conversationOwnerResolver.ResolveAsync(tenantId, userId, cancellationToken);
        var (items, total) = await _conversationRepository.GetPagedByUserAsync(
            tenantId,
            effectiveUserId,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<ConversationDto>(items.Select(MapConversation).ToList(), total, pageIndex, pageSize);
    }

    public async Task<ConversationDto?> GetByIdAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var effectiveUserId = await _conversationOwnerResolver.ResolveAsync(tenantId, userId, cancellationToken);
        var entity = await _conversationRepository.FindByIdAsync(tenantId, conversationId, cancellationToken);
        if (entity is not null && entity.UserId != effectiveUserId)
        {
            throw new BusinessException("NoPermissionAccessConversation", ErrorCodes.Forbidden);
        }

        return entity is null ? null : MapConversation(entity);
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        ConversationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        entity.UpdateTitle(request.Title.Trim());
        await _conversationRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken)
    {
        _ = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chatMessageRepository.DeleteByConversationAsync(tenantId, conversationId, cancellationToken);
            await _conversationRepository.DeleteAsync(tenantId, conversationId, cancellationToken);
        }, cancellationToken);
    }

    public async Task ClearHistoryAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken)
    {
        var conversation = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        conversation.ResetMessages();
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chatMessageRepository.DeleteByConversationAsync(tenantId, conversationId, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }, cancellationToken);
    }

    public async Task ClearContextAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken)
    {
        var conversation = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        conversation.ClearContext();
        var marker = new ChatMessageEntity(
            tenantId,
            conversationId,
            "system",
            "[CONTEXT_CLEARED]",
            null,
            isContextCleared: true,
            _idGeneratorAccessor.NextId());

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chatMessageRepository.AddAsync(marker, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        bool includeContextMarkers,
        int? limit,
        CancellationToken cancellationToken)
    {
        _ = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        var items = await _chatMessageRepository.GetAllByConversationAsync(tenantId, conversationId, cancellationToken);
        var filtered = includeContextMarkers
            ? items
            : items.Where(x => !x.IsContextCleared).ToList();
        if (limit.HasValue && limit.Value > 0 && filtered.Count > limit.Value)
        {
            filtered = filtered.Skip(filtered.Count - limit.Value).ToList();
        }

        return filtered.Select(MapMessage).ToList();
    }

    public async Task<long> AppendMessageAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        ConversationAppendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        var entity = new ChatMessageEntity(
            tenantId,
            conversationId,
            request.Role.Trim().ToLowerInvariant(),
            request.Content.Trim(),
            string.IsNullOrWhiteSpace(request.Metadata) ? null : request.Metadata.Trim(),
            isContextCleared: false,
            _idGeneratorAccessor.NextId());

        conversation.AddMessage(entity.CreatedAt);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chatMessageRepository.AddAsync(entity, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }, cancellationToken);

        return entity.Id;
    }

    public async Task DeleteMessageAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        long messageId,
        CancellationToken cancellationToken)
    {
        var conversation = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        var message = await _chatMessageRepository.FindByConversationAndIdAsync(tenantId, conversationId, messageId, cancellationToken);
        if (message is null)
        {
            throw new BusinessException("MessageNotFound", ErrorCodes.NotFound);
        }

        var latest = await _chatMessageRepository.GetAllByConversationAsync(tenantId, conversationId, cancellationToken);
        var latestMessageAt = latest
            .Where(x => x.Id != messageId && !x.IsContextCleared)
            .Select(x => (DateTime?)x.CreatedAt)
            .OrderByDescending(x => x)
            .FirstOrDefault();
        if (!message.IsContextCleared)
        {
            conversation.RemoveMessage(latestMessageAt);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chatMessageRepository.DeleteAsync(tenantId, messageId, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }, cancellationToken);
    }

    private async Task<Conversation> RequireConversationAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var effectiveUserId = await _conversationOwnerResolver.ResolveAsync(tenantId, userId, cancellationToken);
        var conversation = await _conversationRepository.FindByIdAsync(tenantId, conversationId, cancellationToken)
            ?? throw new BusinessException("ConversationNotFound", ErrorCodes.NotFound);
        if (conversation.UserId != effectiveUserId)
        {
            throw new BusinessException("NoPermissionAccessConversation", ErrorCodes.Forbidden);
        }

        return conversation;
    }
    private static ConversationDto MapConversation(Conversation entity)
        => new(
            entity.Id,
            entity.AgentId,
            entity.UserId,
            entity.Title,
            entity.CreatedAt,
            entity.LastMessageAt > DateTime.UnixEpoch ? entity.LastMessageAt : null,
            entity.MessageCount);

    private static ChatMessageDto MapMessage(ChatMessageEntity entity)
        => new(
            entity.Id,
            entity.Role,
            entity.Content,
            NormalizeMetadata(entity.Metadata),
            entity.CreatedAt,
            entity.IsContextCleared);

    private static string? NormalizeMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return metadata;
        }

        return StructuredJsonStringUtility.TryNormalizeJsonString(metadata, out var normalizedJson)
            ? normalizedJson
            : metadata;
    }
}
