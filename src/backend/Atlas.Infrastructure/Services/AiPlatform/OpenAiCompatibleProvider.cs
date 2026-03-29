using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OpenAiCompatibleProvider : ILlmProvider, IEmbeddingProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiCompatibleProvider> _logger;
    private readonly AiProviderOption _option;

    public OpenAiCompatibleProvider(
        string providerName,
        AiProviderOption option,
        HttpClient httpClient,
        ILogger<OpenAiCompatibleProvider> logger)
    {
        ProviderName = providerName;
        _option = option;
        _httpClient = httpClient;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(option.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(option.BaseUrl.TrimEnd('/'), UriKind.Absolute);
        }
    }

    public string ProviderName { get; }

    public async Task<ChatCompletionResult> ChatAsync(ChatCompletionRequest request, CancellationToken ct = default)
    {
        var payload = new OpenAiChatRequest(
            request.Model,
            request.Messages.Select(m => new OpenAiMessage(
                m.Role,
                m.Content,
                m.Name,
                m.ToolCallId,
                ToolCalls: null)).ToList(),
            request.Temperature,
            request.MaxTokens,
            Stream: false,
            Tools: request.Tools?.Select(MapToolDefinition).ToList(),
            ToolChoice: request.ToolChoice);

        using var response = await SendAsync("v1/chat/completions", payload, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var body = await JsonSerializer.DeserializeAsync<OpenAiChatResponse>(stream, JsonOptions, ct);
        if (body?.Choices is null || body.Choices.Count == 0)
        {
            throw new InvalidOperationException($"AI provider '{ProviderName}' returned empty choices.");
        }

        var first = body.Choices[0];
        return new ChatCompletionResult(
            first.Message?.Content ?? string.Empty,
            body.Model,
            ProviderName,
            first.FinishReason,
            body.Usage?.PromptTokens,
            body.Usage?.CompletionTokens,
            body.Usage?.TotalTokens,
            first.Message?.ToolCalls?.Select(MapToolCall).ToArray());
    }

    public async IAsyncEnumerable<ChatCompletionChunk> ChatStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new OpenAiChatRequest(
            request.Model,
            request.Messages.Select(m => new OpenAiMessage(
                m.Role,
                m.Content,
                m.Name,
                m.ToolCallId,
                ToolCalls: null)).ToList(),
            request.Temperature,
            request.MaxTokens,
            Stream: true,
            Tools: request.Tools?.Select(MapToolDefinition).ToList(),
            ToolChoice: request.ToolChoice);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpointUri("v1/chat/completions"))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_option.ApiKey))
        {
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _option.ApiKey);
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

        using var response = await SendAsync("v1/embeddings", payload, ct);
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

        return new EmbeddingResult(
            vectors,
            body.Model,
            ProviderName,
            body.Usage?.PromptTokens,
            body.Usage?.TotalTokens);
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

    private async Task<HttpResponseMessage> SendAsync(string relativePath, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpointUri(relativePath))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_option.ApiKey))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _option.ApiKey);
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

    private string BuildEndpointUri(string relativePath)
    {
        var baseUrl = (_option.BaseUrl ?? string.Empty).Trim();
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

        return new Uri(baseUri, normalizedRelative).ToString();
    }

    private sealed record OpenAiChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
        [property: JsonPropertyName("temperature")] float? Temperature,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("tools")] IReadOnlyList<OpenAiTool>? Tools = null,
        [property: JsonPropertyName("tool_choice")] string? ToolChoice = null);

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content,
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
