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
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using AiContent = Microsoft.Extensions.AI.AIContent;
using AiFunction = Microsoft.Extensions.AI.AIFunction;
using AiTool = Microsoft.Extensions.AI.AITool;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiFunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using AiFunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using AiTextContent = Microsoft.Extensions.AI.TextContent;
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
            _provider.ProviderName,
            MapTools(options?.Tools),
            MapToolChoice(options?.ToolMode),
            options?.AllowMultipleToolCalls);
        var result = await _provider.ChatAsync(request, cancellationToken);
        return new ChatResponse(BuildAssistantMessage(result));
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
            _provider.ProviderName,
            MapTools(options?.Tools),
            MapToolChoice(options?.ToolMode),
            options?.AllowMultipleToolCalls);
        await foreach (var chunk in _provider.ChatStreamAsync(request, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(chunk.ContentDelta))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.ContentDelta);
            }

            if (chunk.ToolCalls is { Count: > 0 })
            {
                var contents = chunk.ToolCalls
                    .Select(CreateFunctionCallContent)
                    .Cast<AIContent>()
                    .ToList();
                yield return new ChatResponseUpdate(ChatRole.Assistant, contents);
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey) => this;

    public TService? GetService<TService>(object? key = null)
        where TService : class => this as TService;

    public void Dispose()
    {
    }

    private static AiChatMessage BuildAssistantMessage(ChatCompletionResult result)
    {
        var contents = new List<AiContent>();
        if (!string.IsNullOrWhiteSpace(result.Content))
        {
            contents.Add(new AiTextContent(result.Content));
        }

        if (result.ToolCalls is { Count: > 0 })
        {
            contents.AddRange(result.ToolCalls.Select(toolCall => (AiContent)CreateFunctionCallContent(toolCall)));
        }

        return contents.Count == 0
            ? new AiChatMessage(ChatRole.Assistant, string.Empty)
            : new AiChatMessage(ChatRole.Assistant, contents);
    }

    private static AiFunctionCallContent CreateFunctionCallContent(ChatToolCall toolCall)
        => new(
            string.IsNullOrWhiteSpace(toolCall.Id) ? Guid.NewGuid().ToString("N") : toolCall.Id,
            toolCall.Name,
            ParseArguments(toolCall.ArgumentsJson));

    private static IReadOnlyList<ChatMessageModel> MapMessages(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        var mapped = new List<ChatMessageModel>();
        foreach (var message in messages)
        {
            var role = message.Role.ToString().ToLowerInvariant();
            var text = ExtractText(message);
            var toolCalls = message.Contents
                .OfType<AiFunctionCallContent>()
                .Select(content => new ChatToolCall(
                    string.IsNullOrWhiteSpace(content.CallId) ? Guid.NewGuid().ToString("N") : content.CallId,
                    content.Name,
                    SerializeArguments(content.Arguments)))
                .ToList();

            if (message.Contents.OfType<AiFunctionResultContent>().Any())
            {
                foreach (var functionResult in message.Contents.OfType<AiFunctionResultContent>())
                {
                    mapped.Add(new ChatMessageModel(
                        "tool",
                        SerializeToolResult(functionResult.Result),
                        message.AuthorName,
                        functionResult.CallId));
                }

                if (string.IsNullOrWhiteSpace(text) && toolCalls.Count == 0)
                {
                    continue;
                }
            }

            mapped.Add(new ChatMessageModel(
                role,
                string.IsNullOrWhiteSpace(text) ? null : text,
                message.AuthorName,
                ToolCalls: toolCalls.Count == 0 ? null : toolCalls));
        }

        return mapped;
    }

    private static IReadOnlyList<ChatToolDefinition>? MapTools(IEnumerable<AiTool>? tools)
    {
        if (tools is null)
        {
            return null;
        }

        return tools
            .Select(MapTool)
            .Where(item => item is not null)
            .Cast<ChatToolDefinition>()
            .ToList();
    }

    private static ChatToolDefinition? MapTool(AiTool tool)
    {
        var function = tool.GetService<AiFunction>();
        if (function is null)
        {
            return null;
        }

        var parametersJson = NormalizeJsonSchema(function.JsonSchema);
        return new ChatToolDefinition(
            function.Name,
            function.Description ?? tool.Description ?? function.Name,
            parametersJson);
    }

    private static string? MapToolChoice(ChatToolMode? toolMode)
    {
        if (toolMode is null || ReferenceEquals(toolMode, ChatToolMode.Auto))
        {
            return "auto";
        }

        if (ReferenceEquals(toolMode, ChatToolMode.None))
        {
            return "none";
        }

        if (ReferenceEquals(toolMode, ChatToolMode.RequireAny))
        {
            return "required";
        }

        if (toolMode is RequiredChatToolMode requiredChatToolMode)
        {
            return string.IsNullOrWhiteSpace(requiredChatToolMode.RequiredFunctionName)
                ? "required"
                : $"required:{requiredChatToolMode.RequiredFunctionName}";
        }

        return "auto";
    }

    private static string ExtractText(Microsoft.Extensions.AI.ChatMessage message)
    {
        if (message.Contents.Count == 0)
        {
            return message.Text ?? string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var textContent in message.Contents.OfType<AiTextContent>())
        {
            if (string.IsNullOrWhiteSpace(textContent.Text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(textContent.Text);
        }

        return builder.ToString();
    }

    private static string SerializeToolResult(object? result)
    {
        if (result is null)
        {
            return "null";
        }

        if (result is string text)
        {
            return text;
        }

        return JsonSerializer.Serialize(result);
    }

    private static string SerializeArguments(object? arguments)
        => JsonSerializer.Serialize(arguments);

    private static string NormalizeJsonSchema(object? schema)
    {
        if (schema is null)
        {
            return """{"type":"object","properties":{}}""";
        }

        if (schema is string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? """{"type":"object","properties":{}}"""
                : text;
        }

        if (schema is JsonElement jsonElement)
        {
            return jsonElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? """{"type":"object","properties":{}}"""
                : jsonElement.GetRawText();
        }

        return JsonSerializer.Serialize(schema);
    }

    private static Dictionary<string, object?> ParseArguments(string argumentsJson)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            if (parsed is null)
            {
                return [];
            }

            return parsed.ToDictionary(
                pair => pair.Key,
                pair => (object?)ConvertJsonElement(pair.Value));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => (object?)ConvertJsonElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonElement)
                .ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => string.Empty
        };
    }
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
        TeamAgentRuntimePattern runtimePattern,
        IReadOnlyList<TeamAgentMemberItem> members,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = members;
        cancellationToken.ThrowIfCancellationRequested();
        var options = _optionsMonitor.CurrentValue;
        var descriptor = runtimePattern switch
        {
            TeamAgentRuntimePattern.Concurrent => new TeamAgentRuntimeDescriptor(
                "semantic-kernel.concurrent",
                "Semantic Kernel Concurrent Orchestration",
                "Semantic Kernel",
                options.Packages.SemanticKernelOrchestration.PackageId,
                options.Packages.SemanticKernelOrchestration.Version),
            _ => mode switch
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
            }
        };
        return Task.FromResult(descriptor);
    }
}
