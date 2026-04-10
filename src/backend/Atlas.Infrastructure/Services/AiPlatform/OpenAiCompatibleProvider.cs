using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OpenAiCompatibleProvider : ILlmProvider, IEmbeddingProvider
{
    /// <summary>
    /// 多数 OpenAI 兼容端点（含 DeepSeek）要求 max_tokens 落在 [1, 8192]；
    /// UI/配置可能写入 0（表示“默认”），序列化后会被对方拒绝，需省略或截断。
    /// </summary>
    private const int OpenAiCompatibleMaxTokensUpperBound = 8192;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly IReadOnlyDictionary<string, string> DefaultBaseUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] = "https://api.openai.com",
        ["deepseek"] = "https://api.deepseek.com",
        ["ollama"] = "http://localhost:11434"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiCompatibleProvider> _logger;
    private readonly AiProviderOption _option;
    private readonly string _resolvedBaseUrl;
    private readonly TenantId _tenantId;
    private readonly IMeteringService _meteringService;

    public OpenAiCompatibleProvider(
        string providerName,
        AiProviderOption option,
        HttpClient httpClient,
        TenantId tenantId,
        IMeteringService meteringService,
        ILogger<OpenAiCompatibleProvider> logger)
    {
        ProviderName = providerName;
        _option = option;
        _httpClient = httpClient;
        _resolvedBaseUrl = ResolveProviderBaseUrl(providerName, option.BaseUrl);
        _tenantId = tenantId;
        _meteringService = meteringService;
        _logger = logger;

        if (Uri.TryCreate(AppendTrailingSlash(_resolvedBaseUrl), UriKind.Absolute, out var baseAddress))
        {
            _httpClient.BaseAddress = baseAddress;
        }
    }

    public string ProviderName { get; }

    public async Task<ChatCompletionResult> ChatAsync(ChatCompletionRequest request, CancellationToken ct = default)
    {
        var payload = new OpenAiChatRequest(
            request.Model,
            request.Messages.Select(MapMessage).ToList(),
            request.Temperature,
            NormalizeMaxTokensForOpenAiCompatible(request.MaxTokens),
            Stream: false,
            Tools: request.Tools?.Select(MapToolDefinition).ToList(),
            ToolChoice: MapToolChoice(request.ToolChoice),
            ParallelToolCalls: request.AllowParallelToolCalls);

        using var response = await SendAsync("v1/chat/completions", payload, request.Endpoint, request.ApiKey, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var body = await JsonSerializer.DeserializeAsync<OpenAiChatResponse>(stream, JsonOptions, ct);
        if (body?.Choices is null || body.Choices.Count == 0)
        {
            throw new InvalidOperationException($"AI provider '{ProviderName}' returned empty choices.");
        }

        var first = body.Choices[0];
        var completionResult = new ChatCompletionResult(
            first.Message?.Content ?? string.Empty,
            body.Model,
            ProviderName,
            first.FinishReason,
            body.Usage?.PromptTokens,
            body.Usage?.CompletionTokens,
            body.Usage?.TotalTokens,
            first.Message?.ToolCalls?.Select(MapToolCall).ToArray());
        await TryRecordUsageAsync(
            request,
            completionResult.PromptTokens ?? 0,
            completionResult.CompletionTokens ?? 0,
            completionResult.TotalTokens ?? 0,
            ct);
        return completionResult;
    }

    public async IAsyncEnumerable<ChatCompletionChunk> ChatStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new OpenAiChatRequest(
            request.Model,
            request.Messages.Select(MapMessage).ToList(),
            request.Temperature,
            NormalizeMaxTokensForOpenAiCompatible(request.MaxTokens),
            Stream: true,
            Tools: request.Tools?.Select(MapToolDefinition).ToList(),
            ToolChoice: MapToolChoice(request.ToolChoice),
            ParallelToolCalls: request.AllowParallelToolCalls);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CreateRequestUri("v1/chat/completions", request.Endpoint))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var apiKey = ResolveApiKey(request.ApiKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var rawBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"AI provider '{ProviderName}' chat stream failed: {(int)response.StatusCode} {rawBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                break;
            }
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
            {
                yield return new ChatCompletionChunk(string.Empty, true, "stop");
                yield break;
            }

            OpenAiChatStreamResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAiChatStreamResponse>(data, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse stream chunk from provider {ProviderName}.", ProviderName);
                continue;
            }

            if (chunk?.Choices is null || chunk.Choices.Count == 0)
            {
                continue;
            }

            var choice = chunk.Choices[0];
            var delta = choice.Delta?.Content ?? string.Empty;
            var toolCalls = choice.Delta?.ToolCalls?.Select(MapToolCall).ToArray();
            if (delta.Length > 0 || !string.IsNullOrWhiteSpace(choice.FinishReason))
            {
                yield return new ChatCompletionChunk(
                    delta,
                    !string.IsNullOrWhiteSpace(choice.FinishReason),
                    choice.FinishReason,
                    toolCalls);
            }
        }
    }

    public async Task<EmbeddingResult> EmbedAsync(EmbeddingRequest request, CancellationToken ct = default)
    {
        var payload = new OpenAiEmbeddingRequest(
            request.Model,
            request.Inputs.ToList(),
            request.Dimensions);

        using var response = await SendAsync("v1/embeddings", payload, request.Endpoint, request.ApiKey, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var body = await JsonSerializer.DeserializeAsync<OpenAiEmbeddingResponse>(stream, JsonOptions, ct);
        if (body?.Data is null || body.Data.Count == 0)
        {
            throw new InvalidOperationException($"AI provider '{ProviderName}' returned empty embeddings.");
        }

        var vectors = body.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding.ToArray())
            .ToArray();

        var embeddingResult = new EmbeddingResult(
            vectors,
            body.Model,
            ProviderName,
            body.Usage?.PromptTokens,
            body.Usage?.TotalTokens);
        await TryRecordUsageAsync(
            new ChatCompletionRequest(
                request.Model,
                [],
                Provider: "embedding"),
            promptTokens: body.Usage?.PromptTokens ?? 0,
            completionTokens: 0,
            totalTokens: body.Usage?.TotalTokens ?? 0,
            ct);
        return embeddingResult;
    }

    private static OpenAiTool MapToolDefinition(ChatToolDefinition definition)
    {
        JsonElement parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<JsonElement>(definition.ParametersJson);
        }
        catch (JsonException)
        {
            parameters = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new { }
            });
        }

        return new OpenAiTool(
            "function",
            new OpenAiToolFunction(
                definition.Name,
                definition.Description,
                parameters));
    }

    private static ChatToolCall MapToolCall(OpenAiToolCall call)
    {
        return new ChatToolCall(
            call.Id ?? string.Empty,
            call.Function?.Name ?? string.Empty,
            call.Function?.Arguments ?? "{}");
    }

    private static int? NormalizeMaxTokensForOpenAiCompatible(int? maxTokens)
    {
        if (maxTokens is null or <= 0)
        {
            return null;
        }

        return Math.Min(maxTokens.Value, OpenAiCompatibleMaxTokensUpperBound);
    }

    private static OpenAiMessage MapMessage(ChatMessage message)
    {
        return new OpenAiMessage(
            message.Role,
            message.Content,
            message.Name,
            message.ToolCallId,
            message.ToolCalls?.Select(call => new OpenAiToolCall(
                call.Id,
                "function",
                new OpenAiToolCallFunction(call.Name, call.ArgumentsJson))).ToList());
    }

    private static object? MapToolChoice(string? toolChoice)
    {
        if (string.IsNullOrWhiteSpace(toolChoice))
        {
            return null;
        }

        var normalized = toolChoice.Trim();
        if (string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return "auto";
        }

        if (string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        if (string.Equals(normalized, "required", StringComparison.OrdinalIgnoreCase))
        {
            return "required";
        }

        const string requiredPrefix = "required:";
        const string functionPrefix = "function:";
        if (normalized.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[requiredPrefix.Length..].Trim();
        }
        else if (normalized.StartsWith(functionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[functionPrefix.Length..].Trim();
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? "required"
            : new
            {
                type = "function",
                function = new
                {
                    name = normalized
                }
            };
    }

    private async Task TryRecordUsageAsync(
        ChatCompletionRequest request,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        CancellationToken cancellationToken)
    {
        if (totalTokens <= 0 || _tenantId.IsEmpty)
        {
            return;
        }

        try
        {
            var estimatedCost = EstimateCostUsd(promptTokens, completionTokens);
            await _meteringService.RecordLlmUsageAsync(
                _tenantId,
                new LlmUsageRecordCreateRequest(
                    ProviderName,
                    request.Model,
                    request.Provider,
                    promptTokens,
                    completionTokens,
                    totalTokens,
                    estimatedCost),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Record LLM usage failed, provider={ProviderName}", ProviderName);
        }
    }

    private static decimal EstimateCostUsd(int promptTokens, int completionTokens)
    {
        const decimal promptRate = 0.0000005m;
        const decimal completionRate = 0.0000015m;
        var cost = (promptTokens * promptRate) + (completionTokens * completionRate);
        return Math.Round(cost, 8);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string relativePath,
        object payload,
        string? endpointOverride,
        string? apiKeyOverride,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, CreateRequestUri(relativePath, endpointOverride))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var apiKey = ResolveApiKey(apiKeyOverride);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        response.Dispose();
        throw new HttpRequestException($"AI provider '{ProviderName}' request failed: {(int)response.StatusCode} {body}");
    }

    private string CreateRequestUri(string relativePath, string? endpointOverride)
    {
        var requestUri = BuildEndpointUri(relativePath, endpointOverride);
        var hasBaseAddress = string.IsNullOrWhiteSpace(endpointOverride) && _httpClient.BaseAddress is not null;
        if (hasBaseAddress || Uri.TryCreate(requestUri, UriKind.Absolute, out _))
        {
            return requestUri;
        }

        throw new InvalidOperationException(
            $"AI provider '{ProviderName}' 缺少有效的 BaseUrl，无法解析请求地址。请为模型配置填写绝对地址，或使用系统内置提供商默认地址。");
    }

    private string BuildEndpointUri(string relativePath, string? endpointOverride)
    {
        var baseUrl = ResolveRequestBaseUrl(endpointOverride);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return relativePath;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return relativePath;
        }

        var basePath = (baseUri.AbsolutePath ?? string.Empty).TrimEnd('/').ToLowerInvariant();
        var endpointPath = relativePath.Trim().TrimStart('/').ToLowerInvariant();

        // 允许直接填写完整 endpoint（例如 .../v4/chat/completions）。
        if (endpointPath.Contains("chat/completions", StringComparison.Ordinal)
            && basePath.Contains("/chat/completions", StringComparison.Ordinal))
        {
            return baseUri.ToString();
        }
        if (endpointPath.Contains("embeddings", StringComparison.Ordinal)
            && basePath.Contains("/embeddings", StringComparison.Ordinal))
        {
            return baseUri.ToString();
        }

        // 若 BaseUrl 已包含版本前缀（/v1、/v4），避免再拼接重复版本段。
        var normalizedRelative = relativePath.Trim().TrimStart('/');
        if ((basePath.EndsWith("/v1", StringComparison.Ordinal) || basePath.EndsWith("/v4", StringComparison.Ordinal))
            && normalizedRelative.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            var slashIndex = normalizedRelative.IndexOf('/');
            if (slashIndex > 0)
            {
                normalizedRelative = normalizedRelative[(slashIndex + 1)..];
            }
        }

        return new Uri(new Uri(AppendTrailingSlash(baseUri.ToString()), UriKind.Absolute), normalizedRelative).ToString();
    }

    private string ResolveRequestBaseUrl(string? endpointOverride)
    {
        if (!string.IsNullOrWhiteSpace(endpointOverride))
        {
            return endpointOverride.Trim();
        }

        return _resolvedBaseUrl.Trim();
    }

    private string? ResolveApiKey(string? apiKeyOverride)
    {
        if (!string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            return apiKeyOverride.Trim();
        }

        return string.IsNullOrWhiteSpace(_option.ApiKey) ? null : _option.ApiKey.Trim();
    }

    private static string ResolveProviderBaseUrl(string providerName, string? configuredBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.Trim();
        }

        return DefaultBaseUrls.TryGetValue(providerName, out var value)
            ? value
            : string.Empty;
    }

    private static string AppendTrailingSlash(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return url.TrimEnd('/') + "/";
    }

    private sealed record OpenAiChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
        [property: JsonPropertyName("temperature")] float? Temperature,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("tools")] IReadOnlyList<OpenAiTool>? Tools = null,
        [property: JsonPropertyName("tool_choice")] object? ToolChoice = null,
        [property: JsonPropertyName("parallel_tool_calls")] bool? ParallelToolCalls = null);

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("tool_call_id")] string? ToolCallId,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<OpenAiToolCall>? ToolCalls);

    private sealed record OpenAiTool(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] OpenAiToolFunction Function);

    private sealed record OpenAiToolFunction(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] JsonElement Parameters);

    private sealed record OpenAiToolCall(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("function")] OpenAiToolCallFunction? Function);

    private sealed record OpenAiToolCallFunction(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("arguments")] string? Arguments);

    private sealed record OpenAiChatResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChoice>? Choices,
        [property: JsonPropertyName("usage")] OpenAiUsage? Usage);

    private sealed record OpenAiChoice(
        [property: JsonPropertyName("message")] OpenAiMessage? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record OpenAiChatStreamResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiStreamChoice>? Choices);

    private sealed record OpenAiStreamChoice(
        [property: JsonPropertyName("delta")] OpenAiDelta? Delta,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record OpenAiDelta(
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<OpenAiToolCall>? ToolCalls);

    private sealed record OpenAiEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input,
        [property: JsonPropertyName("dimensions")] int? Dimensions = null);

    private sealed record OpenAiEmbeddingResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("data")] IReadOnlyList<OpenAiEmbeddingData>? Data,
        [property: JsonPropertyName("usage")] OpenAiEmbeddingUsage? Usage);

    private sealed record OpenAiEmbeddingData(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] IReadOnlyList<float> Embedding);

    private sealed record OpenAiUsage(
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int? CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);

    private sealed record OpenAiEmbeddingUsage(
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);
}
