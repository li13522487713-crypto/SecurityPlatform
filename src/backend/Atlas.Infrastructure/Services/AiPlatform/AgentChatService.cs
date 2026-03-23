using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ChatMessageEntity = Atlas.Domain.AiPlatform.Entities.ChatMessage;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentChatService : IAgentChatService
{
    private const int ContextWindowSize = 20;
    private const int MaxToolCallIterations = 3;
    private static readonly ConcurrentDictionary<long, CancellationTokenSource> ConversationCancellationMap = new();

    private readonly AgentRepository _agentRepository;
    private readonly ConversationRepository _conversationRepository;
    private readonly ChatMessageRepository _chatMessageRepository;
    private readonly AgentKnowledgeLinkRepository _agentKnowledgeLinkRepository;
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IRagRetrievalService _ragRetrievalService;
    private readonly IAgentToolCallService _agentToolCallService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AgentChatService> _logger;

    public AgentChatService(
        AgentRepository agentRepository,
        ConversationRepository conversationRepository,
        ChatMessageRepository chatMessageRepository,
        AgentKnowledgeLinkRepository agentKnowledgeLinkRepository,
        ModelConfigRepository modelConfigRepository,
        ILlmProviderFactory llmProviderFactory,
        IRagRetrievalService ragRetrievalService,
        IAgentToolCallService agentToolCallService,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        ILogger<AgentChatService> logger)
    {
        _agentRepository = agentRepository;
        _conversationRepository = conversationRepository;
        _chatMessageRepository = chatMessageRepository;
        _agentKnowledgeLinkRepository = agentKnowledgeLinkRepository;
        _modelConfigRepository = modelConfigRepository;
        _llmProviderFactory = llmProviderFactory;
        _ragRetrievalService = ragRetrievalService;
        _agentToolCallService = agentToolCallService;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ChatAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            tenantId,
            userId,
            agentId,
            request,
            textStreamOutput: null,
            eventStreamOutput: null,
            cancellationToken);
        return result;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<string>();
        var producer = Task.Run(async () =>
        {
            try
            {
                await ExecuteAsync(
                    tenantId,
                    userId,
                    agentId,
                    request,
                    chunk => channel.Writer.WriteAsync(chunk, cancellationToken),
                    eventStreamOutput: null,
                    cancellationToken);
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, cancellationToken);

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }

        await producer;
    }

    public async IAsyncEnumerable<AgentChatStreamEvent> ChatEventStreamAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<AgentChatStreamEvent>();
        var producer = Task.Run(async () =>
        {
            try
            {
                await ExecuteAsync(
                    tenantId,
                    userId,
                    agentId,
                    request,
                    textStreamOutput: null,
                    eventStreamOutput: evt => channel.Writer.WriteAsync(evt, cancellationToken),
                    cancellationToken);
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, cancellationToken);

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        await producer;
    }

    public async Task CancelAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.FindByIdAsync(tenantId, conversationId, cancellationToken)
            ?? throw new BusinessException("ConversationNotFound", ErrorCodes.NotFound);

        if (conversation.UserId != userId)
        {
            throw new BusinessException("ConversationForbidden", ErrorCodes.Forbidden);
        }

        if (conversation.AgentId != agentId)
        {
            throw new BusinessException("ConversationAgentMismatch", ErrorCodes.ValidationError);
        }

        if (ConversationCancellationMap.TryRemove(conversationId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task<AgentChatResponse> ExecuteAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        Func<string, ValueTask>? textStreamOutput,
        Func<AgentChatStreamEvent, ValueTask>? eventStreamOutput,
        CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.FindByIdAsync(tenantId, agentId, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);
        var conversation = await EnsureConversationAsync(tenantId, userId, agent, request, cancellationToken);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        while (true)
        {
            if (ConversationCancellationMap.TryGetValue(conversation.Id, out var existing))
            {
                if (!ConversationCancellationMap.TryUpdate(conversation.Id, linkedCts, existing))
                {
                    continue;
                }

                existing.Cancel();
                existing.Dispose();
                break;
            }

            if (ConversationCancellationMap.TryAdd(conversation.Id, linkedCts))
            {
                break;
            }
        }

        try
        {
            var userMessageEntity = new ChatMessageEntity(
                tenantId,
                conversation.Id,
                "user",
                request.Message.Trim(),
                metadata: null,
                isContextCleared: false,
                _idGeneratorAccessor.NextId());

            conversation.AddMessage(userMessageEntity.CreatedAt);
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _chatMessageRepository.AddAsync(userMessageEntity, linkedCts.Token);
                await _conversationRepository.UpdateAsync(conversation, linkedCts.Token);
            }, linkedCts.Token);

            var history = await _chatMessageRepository.GetByConversationAsync(
                tenantId,
                conversation.Id,
                afterContextClear: true,
                limit: ContextWindowSize,
                linkedCts.Token);
            var assistantBuilder = new StringBuilder();
            string? metadata;
            var finalEventEmitted = false;

            var toolCallResult = await _agentToolCallService.TryExecuteAsync(
                tenantId,
                agent.Id,
                request.Message,
                MaxToolCallIterations,
                linkedCts.Token);
            if (toolCallResult.Executed && !string.IsNullOrWhiteSpace(toolCallResult.FinalAnswer))
            {
                foreach (var step in toolCallResult.Steps)
                {
                    await EmitEventAsync(eventStreamOutput, step.EventType, step.Data);
                }
                finalEventEmitted = toolCallResult.Steps.Any(step =>
                    string.Equals(step.EventType, "final", StringComparison.OrdinalIgnoreCase));

                assistantBuilder.Append(toolCallResult.FinalAnswer);
                if (textStreamOutput is not null)
                {
                    await textStreamOutput(toolCallResult.FinalAnswer);
                }

                metadata = JsonSerializer.Serialize(new
                {
                    mode = "tool_call",
                    ragEnabled = false,
                    toolCallMetadata = toolCallResult.MetadataJson
                });
            }
            else
            {
                await EmitEventAsync(eventStreamOutput, "thought", "分析问题并准备调用模型生成回复。");

                var ragResults = await TrySearchRagAsync(tenantId, agent.Id, request, linkedCts.Token);
                var llmMessages = BuildLlmMessages(agent, history, ragResults);

                var modelConfig = await ResolveModelConfigAsync(tenantId, agent.ModelConfigId, linkedCts.Token);
                var providerName = modelConfig?.ProviderType;
                var modelName = ResolveModelName(agent, modelConfig);
                var completionRequest = new ChatCompletionRequest(
                    modelName,
                    llmMessages,
                    Temperature: agent.Temperature,
                    MaxTokens: agent.MaxTokens,
                    Provider: providerName);
                var provider = _llmProviderFactory.GetLlmProvider(providerName);

                await foreach (var chunk in provider.ChatStreamAsync(completionRequest, linkedCts.Token))
                {
                    if (string.IsNullOrWhiteSpace(chunk.ContentDelta))
                    {
                        continue;
                    }

                    assistantBuilder.Append(chunk.ContentDelta);
                    if (textStreamOutput is not null)
                    {
                        await textStreamOutput(chunk.ContentDelta);
                    }
                }

                metadata = JsonSerializer.Serialize(new
                {
                    mode = "llm",
                    provider = provider.ProviderName,
                    model = modelName,
                    ragEnabled = request.EnableRag ?? false,
                    ragSources = ragResults.Select(x => new
                    {
                        x.KnowledgeBaseId,
                        x.DocumentId,
                        x.ChunkId,
                        x.Score
                    }).ToList()
                });
            }

            var assistantContent = assistantBuilder.ToString();
            if (string.IsNullOrWhiteSpace(assistantContent))
            {
                throw new BusinessException("ModelEmptyResponse", ErrorCodes.ServerError);
            }

            if (!finalEventEmitted)
            {
                await EmitEventAsync(eventStreamOutput, "final", assistantContent);
            }

            var assistantMessageId = _idGeneratorAccessor.NextId();
            var assistantMessageEntity = new ChatMessageEntity(
                tenantId,
                conversation.Id,
                "assistant",
                assistantContent,
                metadata,
                isContextCleared: false,
                assistantMessageId);

            conversation.AddMessage(assistantMessageEntity.CreatedAt);
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _chatMessageRepository.AddAsync(assistantMessageEntity, linkedCts.Token);
                await _conversationRepository.UpdateAsync(conversation, linkedCts.Token);
            }, linkedCts.Token);

            return new AgentChatResponse(
                conversation.Id,
                assistantMessageId,
                assistantContent,
                Sources: null);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            _logger.LogInformation("Conversation {ConversationId} canceled.", conversation.Id);
            throw;
        }
        finally
        {
            if (ConversationCancellationMap.TryGetValue(conversation.Id, out var current)
                && ReferenceEquals(current, linkedCts))
            {
                ConversationCancellationMap.TryRemove(conversation.Id, out _);
            }

            linkedCts.Dispose();
        }
    }

    private static async ValueTask EmitEventAsync(
        Func<AgentChatStreamEvent, ValueTask>? eventStreamOutput,
        string eventType,
        string data)
    {
        if (eventStreamOutput is null)
        {
            return;
        }

        await eventStreamOutput(new AgentChatStreamEvent(eventType, data));
    }

    private async Task<Conversation> EnsureConversationAsync(
        TenantId tenantId,
        long userId,
        Agent agent,
        AgentChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConversationId.HasValue)
        {
            var existing = await _conversationRepository.FindByIdAsync(tenantId, request.ConversationId.Value, cancellationToken)
                ?? throw new BusinessException("ConversationNotFound", ErrorCodes.NotFound);
            if (existing.AgentId != agent.Id)
            {
                throw new BusinessException("ConversationAgentMismatch", ErrorCodes.ValidationError);
            }

            if (existing.UserId != userId)
            {
                throw new BusinessException("ConversationAccessDenied", ErrorCodes.Forbidden);
            }

            return existing;
        }

        var title = BuildConversationTitle(request.Message);
        var created = new Conversation(
            tenantId,
            agent.Id,
            userId,
            title,
            _idGeneratorAccessor.NextId());
        await _conversationRepository.AddAsync(created, cancellationToken);
        return created;
    }

    private static IReadOnlyList<Atlas.Application.AiPlatform.Models.ChatMessage> BuildLlmMessages(
        Agent agent,
        IReadOnlyList<ChatMessageEntity> history,
        IReadOnlyList<RagSearchResult> ragResults)
    {
        var messages = new List<Atlas.Application.AiPlatform.Models.ChatMessage>(history.Count + 2);
        if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
        {
            messages.Add(new Atlas.Application.AiPlatform.Models.ChatMessage("system", agent.SystemPrompt));
        }

        if (ragResults.Count > 0)
        {
            var contextText = string.Join(
                "\n\n",
                ragResults.Select((x, i) =>
                    $"[RAG#{i + 1}] (kb:{x.KnowledgeBaseId}, doc:{x.DocumentId}, chunk:{x.ChunkId}, score:{x.Score:F4})\n{x.Content}"));
            messages.Add(new Atlas.Application.AiPlatform.Models.ChatMessage(
                "system",
                $"你可以参考以下知识库检索结果来回答用户问题。请优先使用其中事实，并在适当时给出简短来源说明。\n\n{contextText}"));
        }

        foreach (var item in history)
        {
            messages.Add(new Atlas.Application.AiPlatform.Models.ChatMessage(item.Role, item.Content));
        }

        return messages;
    }

    private async Task<IReadOnlyList<RagSearchResult>> TrySearchRagAsync(
        TenantId tenantId,
        long agentId,
        AgentChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!(request.EnableRag ?? false))
        {
            return [];
        }

        var links = await _agentKnowledgeLinkRepository.GetByAgentIdAsync(tenantId, agentId, cancellationToken);
        if (links.Count == 0)
        {
            return [];
        }

        var knowledgeBaseIds = links.Select(x => x.KnowledgeBaseId).Distinct().ToList();
        return await _ragRetrievalService.SearchAsync(
            tenantId,
            knowledgeBaseIds,
            request.Message,
            topK: 5,
            cancellationToken);
    }

    private async Task<ModelConfig?> ResolveModelConfigAsync(
        TenantId tenantId,
        long? modelConfigId,
        CancellationToken cancellationToken)
    {
        if (!modelConfigId.HasValue || modelConfigId.Value <= 0)
        {
            return null;
        }

        return await _modelConfigRepository.FindByIdAsync(tenantId, modelConfigId.Value, cancellationToken);
    }

    private static string ResolveModelName(Agent agent, ModelConfig? modelConfig)
    {
        if (!string.IsNullOrWhiteSpace(agent.ModelName))
        {
            return agent.ModelName;
        }

        if (modelConfig is not null && !string.IsNullOrWhiteSpace(modelConfig.DefaultModel))
        {
            return modelConfig.DefaultModel;
        }

        return "gpt-4o-mini";
    }

    private static string BuildConversationTitle(string message)
    {
        var normalized = message.Trim();
        if (normalized.Length <= 20)
        {
            return normalized;
        }

        return $"{normalized[..20]}...";
    }
}
