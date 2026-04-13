#pragma warning disable SKEXP0001, SKEXP0110, SKEXP0130
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DomainAgent = Atlas.Domain.AiPlatform.Entities.Agent;
using ChatMessageEntity = Atlas.Domain.AiPlatform.Entities.ChatMessage;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentChatService : IAgentChatService
{
    private sealed record ExecutionStreamItem(
        string? TextChunk,
        AgentChatStreamEvent? Event,
        AgentChatResponse? FinalResponse);

    private static readonly ConcurrentDictionary<long, CancellationTokenSource> ConversationCancellationMap = new();

    private readonly AgentRepository _agentRepository;
    private readonly ConversationRepository _conversationRepository;
    private readonly ChatMessageRepository _chatMessageRepository;
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly IChatClientFactory _chatClientFactory;
    private readonly IKernelFactory _kernelFactory;
    private readonly AgentKernelAugmentationService _agentKernelAugmentationService;
    private readonly ILongTermMemoryExtractionService _longTermMemoryExtractionService;
    private readonly IConversationOwnerResolver _conversationOwnerResolver;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<AgentFrameworkOptions> _optionsMonitor;
    private readonly ILogger<AgentChatService> _logger;

    public AgentChatService(
        AgentRepository agentRepository,
        ConversationRepository conversationRepository,
        ChatMessageRepository chatMessageRepository,
        ModelConfigRepository modelConfigRepository,
        IChatClientFactory chatClientFactory,
        IKernelFactory kernelFactory,
        AgentKernelAugmentationService agentKernelAugmentationService,
        ILongTermMemoryExtractionService longTermMemoryExtractionService,
        IConversationOwnerResolver conversationOwnerResolver,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        IOptionsMonitor<AgentFrameworkOptions> optionsMonitor,
        ILogger<AgentChatService> logger)
    {
        _agentRepository = agentRepository;
        _conversationRepository = conversationRepository;
        _chatMessageRepository = chatMessageRepository;
        _modelConfigRepository = modelConfigRepository;
        _chatClientFactory = chatClientFactory;
        _kernelFactory = kernelFactory;
        _agentKernelAugmentationService = agentKernelAugmentationService;
        _longTermMemoryExtractionService = longTermMemoryExtractionService;
        _conversationOwnerResolver = conversationOwnerResolver;
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
        AgentChatResponse? result = null;
        await foreach (var item in ExecuteCoreStreamAsync(
                           tenantId,
                           userId,
                           agentId,
                           request,
                           emitTextChunks: false,
                           emitStructuredEvents: false,
                           cancellationToken))
        {
            if (item.FinalResponse is not null)
            {
                result = item.FinalResponse;
            }
        }

        return result ?? throw new BusinessException("ModelEmptyResponse", ErrorCodes.ServerError);
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in ExecuteCoreStreamAsync(
                           tenantId,
                           userId,
                           agentId,
                           request,
                           emitTextChunks: true,
                           emitStructuredEvents: false,
                           cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(item.TextChunk))
            {
                yield return item.TextChunk;
            }
        }
    }

    public async IAsyncEnumerable<AgentChatStreamEvent> ChatEventStreamAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in ExecuteCoreStreamAsync(
                           tenantId,
                           userId,
                           agentId,
                           request,
                           emitTextChunks: false,
                           emitStructuredEvents: true,
                           cancellationToken))
        {
            if (item.Event is not null)
            {
                yield return item.Event;
            }
        }
    }

    public async Task CancelAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var effectiveUserId = await _conversationOwnerResolver.ResolveAsync(tenantId, userId, cancellationToken);
        var conversation = await _conversationRepository.FindByIdAsync(tenantId, conversationId, cancellationToken)
            ?? throw new BusinessException("ConversationNotFound", ErrorCodes.NotFound);

        if (conversation.UserId != effectiveUserId)
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

    private async IAsyncEnumerable<ExecutionStreamItem> ExecuteCoreStreamAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        bool emitTextChunks,
        bool emitStructuredEvents,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.FindByIdAsync(tenantId, agentId, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);
        var effectiveUserId = await _conversationOwnerResolver.ResolveAsync(tenantId, userId, cancellationToken);
        var conversation = await EnsureConversationAsync(tenantId, effectiveUserId, agent, request, cancellationToken);

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

            if (emitStructuredEvents)
            {
                yield return new ExecutionStreamItem(
                    TextChunk: null,
                    Event: new AgentChatStreamEvent("thought", "分析问题并准备调用 Semantic Kernel 原生 Agent 与函数能力。"),
                    FinalResponse: null);
            }

            var modelConfig = await ResolveModelConfigAsync(tenantId, agent.ModelConfigId, linkedCts.Token);
            var modelName = ResolveModelName(agent, modelConfig);
            var kernel = await _kernelFactory.CreateAsync(tenantId, agent.ModelConfigId, modelName, linkedCts.Token);
            var chatClient = await _chatClientFactory.CreateAsync(tenantId, agent.ModelConfigId, modelName, linkedCts.Token);
            var augmentationResult = await _agentKernelAugmentationService.ConfigureAsync(
                tenantId,
                agent.Id,
                kernel,
                request.EnableRag ?? false,
                linkedCts.Token);
            var reducedHistory = await BuildReducedChatHistoryAsync(
                history,
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
                Kernel = kernel,
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
                {
                    Temperature = agent.Temperature ?? 0,
                    MaxTokens = agent.MaxTokens ?? 0,
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                        functions: null,
                        autoInvoke: true,
                        options: new FunctionChoiceBehaviorOptions
                        {
                            AllowParallelCalls = true,
                            AllowConcurrentInvocation = true
                        })
                })
            };

            await foreach (var response in skAgent.InvokeStreamingAsync(
                new ChatMessageContent(AuthorRole.User, inputForModel),
                agentThread))
            {
                var chunkText = NormalizeAgentResponse(response.Message);
                if (string.IsNullOrWhiteSpace(chunkText))
                {
                    continue;
                }

                assistantBuilder.Append(chunkText);
                if (emitTextChunks)
                {
                    yield return new ExecutionStreamItem(
                        TextChunk: chunkText,
                        Event: null,
                        FinalResponse: null);
                }
            }

            metadata = JsonSerializer.Serialize(new
            {
                mode = "semantic-kernel.agent",
                provider = modelConfig?.ProviderType ?? "default",
                model = modelName,
                functionCalling = "semantic-kernel.auto",
                boundToolFunctionCount = augmentationResult.BoundToolFunctionCount,
                knowledgePluginEnabled = augmentationResult.KnowledgePluginEnabled,
                memory = new
                {
                    reducer = "semantic-kernel.truncation",
                    whiteboardEnabled = agent.EnableMemory && agent.EnableShortTermMemory && _optionsMonitor.CurrentValue.EnableWhiteboardMemory,
                    longTermMemoryCount = recalledMemories.Count,
                    longTermMemoryIds = recalledMemories.Select(x => x.Id).ToList()
                }
            });

            var assistantContent = assistantBuilder.ToString();
            if (string.IsNullOrWhiteSpace(assistantContent))
            {
                throw new BusinessException("ModelEmptyResponse", ErrorCodes.ServerError);
            }

            if (emitStructuredEvents)
            {
                yield return new ExecutionStreamItem(
                    TextChunk: null,
                    Event: new AgentChatStreamEvent("final", assistantContent),
                    FinalResponse: null);
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

            yield return new ExecutionStreamItem(
                TextChunk: null,
                Event: null,
                FinalResponse: new AgentChatResponse(
                    conversation.Id,
                    assistantMessageId,
                    assistantContent,
                    Sources: null));
            yield break;
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

    private static string NormalizeAgentResponse(object? response)
        => response switch
        {
            ChatMessageContent chatMessageContent => string.IsNullOrWhiteSpace(chatMessageContent.Content)
                ? chatMessageContent.ToString()?.Trim() ?? string.Empty
                : chatMessageContent.Content.Trim(),
            StreamingChatMessageContent streamingChatMessageContent => string.IsNullOrWhiteSpace(streamingChatMessageContent.Content)
                ? streamingChatMessageContent.ToString()?.Trim() ?? string.Empty
                : streamingChatMessageContent.Content.Trim(),
            _ => response?.ToString()?.Trim() ?? string.Empty
        };

    private static AuthorRole MapAuthorRole(string role)
        => role.ToLowerInvariant() switch
        {
            "system" => AuthorRole.System,
            "assistant" => AuthorRole.Assistant,
            "tool" => AuthorRole.Tool,
            _ => AuthorRole.User
        };

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
