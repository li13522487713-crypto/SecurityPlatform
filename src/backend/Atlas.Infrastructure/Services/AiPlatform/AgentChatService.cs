#pragma warning disable SKEXP0001, SKEXP0110, SKEXP0130
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DomainAgent = Atlas.Domain.AiPlatform.Entities.Agent;
using ChatMessageEntity = Atlas.Domain.AiPlatform.Entities.ChatMessage;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentChatService : IAgentChatService
{
    private const int MaxToolCallIterations = 3;
    private static readonly ConcurrentDictionary<long, CancellationTokenSource> ConversationCancellationMap = new();

    private readonly AgentRepository _agentRepository;
    private readonly ConversationRepository _conversationRepository;
    private readonly ChatMessageRepository _chatMessageRepository;
    private readonly AgentKnowledgeLinkRepository _agentKnowledgeLinkRepository;
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly IChatClientFactory _chatClientFactory;
    private readonly IKernelFactory _kernelFactory;
    private readonly IRagRetrievalService _ragRetrievalService;
    private readonly IAgentToolCallService _agentToolCallService;
    private readonly ILongTermMemoryExtractionService _longTermMemoryExtractionService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<AgentFrameworkOptions> _optionsMonitor;
    private readonly ILogger<AgentChatService> _logger;

    public AgentChatService(
        AgentRepository agentRepository,
        ConversationRepository conversationRepository,
        ChatMessageRepository chatMessageRepository,
        AgentKnowledgeLinkRepository agentKnowledgeLinkRepository,
        ModelConfigRepository modelConfigRepository,
        IChatClientFactory chatClientFactory,
        IKernelFactory kernelFactory,
        IRagRetrievalService ragRetrievalService,
        IAgentToolCallService agentToolCallService,
        ILongTermMemoryExtractionService longTermMemoryExtractionService,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        IOptionsMonitor<AgentFrameworkOptions> optionsMonitor,
        ILogger<AgentChatService> logger)
    {
        _agentRepository = agentRepository;
        _conversationRepository = conversationRepository;
        _chatMessageRepository = chatMessageRepository;
        _agentKnowledgeLinkRepository = agentKnowledgeLinkRepository;
        _modelConfigRepository = modelConfigRepository;
        _chatClientFactory = chatClientFactory;
        _kernelFactory = kernelFactory;
        _ragRetrievalService = ragRetrievalService;
        _agentToolCallService = agentToolCallService;
        _longTermMemoryExtractionService = longTermMemoryExtractionService;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _optionsMonitor = optionsMonitor;
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
            var normalizedMessage = request.Message?.Trim() ?? string.Empty;
            var normalizedAttachments = NormalizeAttachments(request.Attachments);
            var inputForModel = BuildUserInput(normalizedMessage, normalizedAttachments);
            var userMessageContent = string.IsNullOrWhiteSpace(normalizedMessage) ? "[多模态输入]" : normalizedMessage;
            var userMessageEntity = new ChatMessageEntity(
                tenantId,
                conversation.Id,
                "user",
                userMessageContent,
                metadata: BuildAttachmentMetadata(normalizedAttachments),
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
                limit: null,
                linkedCts.Token);
            var recalledMemories = agent.EnableMemory && agent.EnableLongTermMemory
                ? await SafeRecallLongTermMemoriesAsync(
                    tenantId,
                    userId,
                    agent.Id,
                    inputForModel,
                    agent.LongTermMemoryTopK,
                    linkedCts.Token)
                : [];
            var assistantBuilder = new StringBuilder();
            string? metadata;
            var finalEventEmitted = false;

            var toolCallResult = await _agentToolCallService.TryExecuteAsync(
                tenantId,
                agent.Id,
                inputForModel,
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
                    toolCallMetadata = toolCallResult.MetadataJson,
                    memory = new
                    {
                        reducer = "semantic-kernel.truncation",
                        whiteboardEnabled = agent.EnableMemory && agent.EnableShortTermMemory && _optionsMonitor.CurrentValue.EnableWhiteboardMemory,
                        longTermMemoryCount = recalledMemories.Count,
                        longTermMemoryIds = recalledMemories.Select(x => x.Id).ToList()
                    }
                });
            }
            else
            {
                await EmitEventAsync(eventStreamOutput, "thought", "分析问题并准备调用模型生成回复。");

                var modelConfig = await ResolveModelConfigAsync(tenantId, agent.ModelConfigId, linkedCts.Token);
                var modelName = ResolveModelName(agent, modelConfig);
                var ragResults = await TrySearchRagAsync(tenantId, agent.Id, request, linkedCts.Token);
                var kernel = await _kernelFactory.CreateAsync(tenantId, agent.ModelConfigId, modelName, linkedCts.Token);
                var chatClient = await _chatClientFactory.CreateAsync(tenantId, agent.ModelConfigId, modelName, linkedCts.Token);
                var reducedHistory = await BuildReducedChatHistoryAsync(
                    history,
                    ragResults,
                    recalledMemories,
                    userMessageEntity.Id,
                    linkedCts.Token);
                var agentThread = new ChatHistoryAgentThread(reducedHistory);
                if (agent.EnableMemory && agent.EnableShortTermMemory && _optionsMonitor.CurrentValue.EnableWhiteboardMemory)
                {
                    agentThread.AIContextProviders.Add(new WhiteboardProvider(chatClient));
                }

                var skAgent = new ChatCompletionAgent
                {
                    Name = agent.Name,
                    Instructions = string.IsNullOrWhiteSpace(agent.SystemPrompt) ? "你是一个有帮助的智能助手。" : agent.SystemPrompt,
                    Kernel = kernel
                };

        await foreach (var response in skAgent.InvokeAsync(
                    new ChatMessageContent(AuthorRole.User, inputForModel),
                    agentThread))
                {
                    var chunkText = NormalizeAgentResponse(response.Message);
                    if (string.IsNullOrWhiteSpace(chunkText))
                    {
                        continue;
                    }

                    assistantBuilder.Clear();
                    assistantBuilder.Append(chunkText);
                }

                if (assistantBuilder.Length > 0 && textStreamOutput is not null)
                {
                    await textStreamOutput(assistantBuilder.ToString());
                }

                metadata = JsonSerializer.Serialize(new
                {
                    mode = "semantic-kernel.agent",
                    provider = modelConfig?.ProviderType ?? "default",
                    model = modelName,
                    ragEnabled = request.EnableRag ?? false,
                    ragSources = ragResults.Select(x => new
                    {
                        x.KnowledgeBaseId,
                        x.DocumentId,
                        x.ChunkId,
                        x.Score
                    }).ToList(),
                    memory = new
                    {
                        reducer = "semantic-kernel.truncation",
                        whiteboardEnabled = agent.EnableMemory && agent.EnableShortTermMemory && _optionsMonitor.CurrentValue.EnableWhiteboardMemory,
                        longTermMemoryCount = recalledMemories.Count,
                        longTermMemoryIds = recalledMemories.Select(x => x.Id).ToList()
                    }
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

            await SafePersistMemoryAsync(
                tenantId,
                userId,
                agent.Id,
                conversation.Id,
                inputForModel,
                assistantContent,
                agent.EnableMemory,
                agent.EnableLongTermMemory,
                linkedCts.Token);

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

    private async Task<IReadOnlyList<LongTermMemoryRecallItem>> SafeRecallLongTermMemoriesAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        string message,
        int longTermTopK,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _longTermMemoryExtractionService.RecallAsync(
                tenantId,
                userId,
                agentId,
                message,
                longTermTopK,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Recall long-term memories failed. userId={UserId}, agentId={AgentId}",
                userId,
                agentId);
            return [];
        }
    }

    private async Task SafePersistMemoryAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        string userMessage,
        string assistantMessage,
        bool enableMemory,
        bool enableLongTermMemory,
        CancellationToken cancellationToken)
    {
        if (!enableMemory)
        {
            return;
        }

        try
        {
            if (enableLongTermMemory)
            {
                await _longTermMemoryExtractionService.ExtractAsync(
                    tenantId,
                    userId,
                    agentId,
                    conversationId,
                    userMessage,
                    assistantMessage,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Persist conversation memory failed. conversationId={ConversationId}",
                conversationId);
        }
    }

    private async Task<Conversation> EnsureConversationAsync(
        TenantId tenantId,
        long userId,
        DomainAgent agent,
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

        var title = BuildConversationTitle(BuildUserInput(request.Message?.Trim(), NormalizeAttachments(request.Attachments)));
        var created = new Conversation(
            tenantId,
            agent.Id,
            userId,
            title,
            _idGeneratorAccessor.NextId());
        await _conversationRepository.AddAsync(created, cancellationToken);
        return created;
    }

    private async Task<ChatHistory> BuildReducedChatHistoryAsync(
        IReadOnlyList<ChatMessageEntity> history,
        IReadOnlyList<RagSearchResult> ragResults,
        IReadOnlyList<LongTermMemoryRecallItem> longTermMemories,
        long pendingUserMessageId,
        CancellationToken cancellationToken)
    {
        var chatHistory = new ChatHistory();

        if (longTermMemories.Count > 0)
        {
            var memoryText = string.Join(
                "\n",
                longTermMemories.Select((item, index) =>
                    $"[MEM#{index + 1}] ({item.MemoryKey}, score:{item.Score:F3}) {item.Content}"));
            chatHistory.AddMessage(
                AuthorRole.System,
                $"以下是用户长期偏好/画像记忆，请在回答时优先遵循：\n{memoryText}");
        }

        if (ragResults.Count > 0)
        {
            var contextText = string.Join(
                "\n\n",
                ragResults.Select((item, index) =>
                    $"[RAG#{index + 1}] (kb:{item.KnowledgeBaseId}, doc:{item.DocumentId}, chunk:{item.ChunkId}, score:{item.Score:F4})\n{item.Content}"));
            chatHistory.AddMessage(
                AuthorRole.System,
                $"你可以参考以下知识库检索结果来回答用户问题。请优先使用其中事实，并在适当时给出简短来源说明。\n\n{contextText}");
        }

        foreach (var item in history.Where(item => item.Id != pendingUserMessageId))
        {
            chatHistory.AddMessage(MapAuthorRole(item.Role), item.Content);
        }

        var reducer = new ChatHistoryTruncationReducer(_optionsMonitor.CurrentValue.SingleAgentReducerTargetCount);
        var reducedMessages = await reducer.ReduceAsync(chatHistory, cancellationToken);
        if (reducedMessages is not null)
        {
            chatHistory = new ChatHistory(reducedMessages);
        }

        return chatHistory;
    }

    private static string NormalizeAgentResponse(ChatMessageContent response)
        => string.IsNullOrWhiteSpace(response.Content)
            ? response.ToString()?.Trim() ?? string.Empty
            : response.Content.Trim();

    private static AuthorRole MapAuthorRole(string role)
        => role.ToLowerInvariant() switch
        {
            "system" => AuthorRole.System,
            "assistant" => AuthorRole.Assistant,
            "tool" => AuthorRole.Tool,
            _ => AuthorRole.User
        };

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
        var query = BuildUserInput(request.Message?.Trim(), NormalizeAttachments(request.Attachments));
        return await _ragRetrievalService.SearchAsync(
            tenantId,
            knowledgeBaseIds,
            query,
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

    private static string ResolveModelName(DomainAgent agent, ModelConfig? modelConfig)
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

    private static IReadOnlyList<AgentChatAttachment> NormalizeAttachments(
        IReadOnlyList<AgentChatAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return [];
        }

        return attachments
            .Where(item => item is not null)
            .Select(item => new AgentChatAttachment(
                Type: item.Type?.Trim() ?? string.Empty,
                Url: string.IsNullOrWhiteSpace(item.Url) ? null : item.Url.Trim(),
                FileId: string.IsNullOrWhiteSpace(item.FileId) ? null : item.FileId.Trim(),
                MimeType: string.IsNullOrWhiteSpace(item.MimeType) ? null : item.MimeType.Trim(),
                Name: string.IsNullOrWhiteSpace(item.Name) ? null : item.Name.Trim(),
                Text: string.IsNullOrWhiteSpace(item.Text) ? null : item.Text.Trim()))
            .Where(item => !string.IsNullOrWhiteSpace(item.Type))
            .ToList();
    }

    private static string? BuildAttachmentMetadata(IReadOnlyList<AgentChatAttachment> attachments)
    {
        if (attachments.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(new
        {
            attachments = attachments.Select(item => new
            {
                type = item.Type,
                url = item.Url,
                fileId = item.FileId,
                mimeType = item.MimeType,
                name = item.Name,
                text = item.Text
            }).ToList()
        });
    }

    private static string BuildUserInput(string? message, IReadOnlyList<AgentChatAttachment> attachments)
    {
        var text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        if (attachments.Count == 0)
        {
            return text;
        }

        var attachmentText = string.Join(
            "\n",
            attachments.Select((item, index) =>
                $"[Attachment#{index + 1}] type={item.Type}, fileId={item.FileId ?? "-"}, url={item.Url ?? "-"}, name={item.Name ?? "-"}, mimeType={item.MimeType ?? "-"}, text={item.Text ?? "-"}"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return attachmentText;
        }

        return $"{text}\n\n{attachmentText}";
    }

    private static string BuildConversationTitle(string message)
    {
        var normalized = message.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "多模态会话";
        }
        if (normalized.Length <= 20)
        {
            return normalized;
        }

        return $"{normalized[..20]}...";
    }
}
