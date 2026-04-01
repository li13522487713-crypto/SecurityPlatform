using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;
using ChatMessageModel = Atlas.Application.AiPlatform.Models.ChatMessage;

namespace Atlas.Infrastructure.Services.AiPlatform;

internal sealed class ProviderBackedChatClient : IChatClient
{
    private readonly ILlmProvider _provider;
    private readonly string _modelId;
    private readonly Uri? _endpoint;

    public ProviderBackedChatClient(ILlmProvider provider, string modelId, string? endpoint)
    {
        _provider = provider;
        _modelId = modelId;
        _endpoint = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri : null;
    }

    public ChatClientMetadata Metadata => new(nameof(ProviderBackedChatClient), _endpoint, _modelId);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatCompletionRequest(
            options?.ModelId ?? _modelId,
            MapMessages(chatMessages),
            options?.Temperature,
            options?.MaxOutputTokens,
            _provider.ProviderName);
        var result = await _provider.ChatAsync(request, cancellationToken);
        return new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, result.Content));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new ChatCompletionRequest(
            options?.ModelId ?? _modelId,
            MapMessages(chatMessages),
            options?.Temperature,
            options?.MaxOutputTokens,
            _provider.ProviderName);
        await foreach (var chunk in _provider.ChatStreamAsync(request, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(chunk.ContentDelta))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.ContentDelta);
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey) => this;

    public TService? GetService<TService>(object? key = null)
        where TService : class => this as TService;

    public void Dispose()
    {
    }

    private static IReadOnlyList<ChatMessageModel> MapMessages(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
        => messages.Select(message => new ChatMessageModel(
            message.Role.ToString().ToLowerInvariant(),
            message.ToString())).ToList();
}

public sealed class ChatClientFactory : IChatClientFactory
{
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly ILlmProviderFactory _llmProviderFactory;

    public ChatClientFactory(
        ModelConfigRepository modelConfigRepository,
        ILlmProviderFactory llmProviderFactory)
    {
        _modelConfigRepository = modelConfigRepository;
        _llmProviderFactory = llmProviderFactory;
    }

    public async Task<IChatClient> CreateAsync(
        TenantId tenantId,
        long? modelConfigId,
        string? modelName,
        CancellationToken cancellationToken)
    {
        ModelConfig? modelConfig = null;
        if (modelConfigId.HasValue && modelConfigId.Value > 0)
        {
            modelConfig = await _modelConfigRepository.FindByIdAsync(tenantId, modelConfigId.Value, cancellationToken);
        }

        var providerName = modelConfig?.ProviderType;
        var provider = _llmProviderFactory.GetLlmProvider(providerName);
        var resolvedModel = !string.IsNullOrWhiteSpace(modelName)
            ? modelName.Trim()
            : !string.IsNullOrWhiteSpace(modelConfig?.DefaultModel)
                ? modelConfig.DefaultModel
                : "gpt-4o-mini";
        return new ProviderBackedChatClient(provider, resolvedModel, modelConfig?.BaseUrl);
    }
}

public sealed class KernelFactory : IKernelFactory
{
    private readonly IChatClientFactory _chatClientFactory;

    public KernelFactory(IChatClientFactory chatClientFactory)
    {
        _chatClientFactory = chatClientFactory;
    }

    public async Task<Kernel> CreateAsync(
        TenantId tenantId,
        long? modelConfigId,
        string? modelName,
        CancellationToken cancellationToken)
    {
        var chatClient = await _chatClientFactory.CreateAsync(tenantId, modelConfigId, modelName, cancellationToken);
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatClient>(chatClient);
        builder.Services.AddSingleton<IChatCompletionService>(serviceProvider => chatClient.AsChatCompletionService(serviceProvider));
        return builder.Build();
    }
}

internal sealed class AgentRuntimeFactory : IAgentRuntimeFactory
{
    private readonly IOptionsMonitor<AgentFrameworkOptions> _optionsMonitor;

    public AgentRuntimeFactory(IOptionsMonitor<AgentFrameworkOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public Task<TeamAgentRuntimeDescriptor> ResolveRuntimeAsync(
        TenantId tenantId,
        TeamAgentMode mode,
        IReadOnlyList<TeamAgentMemberItem> members,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = members;
        cancellationToken.ThrowIfCancellationRequested();
        var options = _optionsMonitor.CurrentValue;
        var descriptor = mode switch
        {
            TeamAgentMode.GroupChat => new TeamAgentRuntimeDescriptor(
                "semantic-kernel.group-chat",
                "Semantic Kernel Agents Orchestration",
                "Semantic Kernel",
                options.Packages.SemanticKernelOrchestration.PackageId,
                options.Packages.SemanticKernelOrchestration.Version),
            TeamAgentMode.Workflow => new TeamAgentRuntimeDescriptor(
                "semantic-kernel.sequential",
                "Semantic Kernel Sequential Orchestration",
                "Semantic Kernel",
                options.Packages.SemanticKernelOrchestration.PackageId,
                options.Packages.SemanticKernelOrchestration.Version),
            _ => new TeamAgentRuntimeDescriptor(
                "semantic-kernel.handoff",
                "Semantic Kernel Handoff Orchestration",
                "Semantic Kernel",
                options.Packages.SemanticKernelOrchestration.PackageId,
                options.Packages.SemanticKernelOrchestration.Version)
        };
        return Task.FromResult(descriptor);
    }
}
