using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ModelConfigCommandService : IModelConfigCommandService
{
    private readonly ModelConfigRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiCompatibleProvider> _providerLogger;

    public ModelConfigCommandService(
        ModelConfigRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiCompatibleProvider> providerLogger)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _httpClientFactory = httpClientFactory;
        _providerLogger = providerLogger;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        ModelConfigCreateRequest request,
        CancellationToken cancellationToken)
    {
        var exists = await _repository.ExistsByNameAsync(tenantId, request.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessException($"模型配置名称 '{request.Name}' 已存在。", ErrorCodes.ValidationError);
        }

        var entity = new ModelConfig(
            tenantId,
            request.Name,
            request.ProviderType,
            request.ApiKey,
            request.BaseUrl,
            request.DefaultModel,
            _idGeneratorAccessor.NextId());
        entity.Update(
            request.Name,
            request.ApiKey,
            request.BaseUrl,
            request.DefaultModel,
            isEnabled: true,
            request.SupportsEmbedding);

        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long id,
        ModelConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("模型配置不存在。", ErrorCodes.NotFound);

        var duplicated = await _repository.FindByNameAsync(tenantId, request.Name, cancellationToken);
        if (duplicated is not null && duplicated.Id != id)
        {
            throw new BusinessException($"模型配置名称 '{request.Name}' 已存在。", ErrorCodes.ValidationError);
        }

        var effectiveApiKey = string.IsNullOrWhiteSpace(request.ApiKey)
            ? entity.ApiKey
            : request.ApiKey;

        entity.Update(
            request.Name,
            effectiveApiKey,
            request.BaseUrl,
            request.DefaultModel,
            request.IsEnabled,
            request.SupportsEmbedding);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("模型配置不存在。", ErrorCodes.NotFound);

        await _repository.DeleteAsync(tenantId, entity.Id, cancellationToken);
    }

    public async Task<ModelConfigTestResult> TestConnectionAsync(
        TenantId tenantId,
        ModelConfigTestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var effectiveApiKey = await ResolveEffectiveApiKeyAsync(
                tenantId,
                request.ModelConfigId,
                request.ApiKey,
                cancellationToken);

            var option = new AiProviderOption
            {
                ApiKey = effectiveApiKey,
                BaseUrl = request.BaseUrl,
                DefaultModel = request.Model,
                SupportsEmbedding = false
            };

            var provider = new OpenAiCompatibleProvider(
                request.ProviderType,
                option,
                _httpClientFactory.CreateClient("AiPlatform"),
                _providerLogger);

            var startedAt = DateTime.UtcNow;
            await provider.ChatAsync(
                new ChatCompletionRequest(
                    request.Model,
                    [new Atlas.Application.AiPlatform.Models.ChatMessage("user", "hi")],
                    Temperature: 0),
                cancellationToken);

            var latency = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;
            return new ModelConfigTestResult(true, null, latency);
        }
        catch (Exception ex)
        {
            return new ModelConfigTestResult(false, ex.Message, null);
        }
    }

    public async IAsyncEnumerable<ModelConfigPromptTestStreamEvent> TestPromptStreamAsync(
        TenantId tenantId,
        ModelConfigPromptTestRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string effectiveApiKey = string.Empty;
        string? apiKeyResolveError = null;
        try
        {
            effectiveApiKey = await ResolveEffectiveApiKeyAsync(
                tenantId,
                request.ModelConfigId,
                request.ApiKey,
                cancellationToken);
        }
        catch (Exception ex)
        {
            apiKeyResolveError = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(apiKeyResolveError))
        {
            yield return new ModelConfigPromptTestStreamEvent("error", apiKeyResolveError);
            yield break;
        }

        OpenAiCompatibleProvider? provider = null;
        string? initError = null;
        try
        {
            var option = new AiProviderOption
            {
                ApiKey = effectiveApiKey,
                BaseUrl = request.BaseUrl,
                DefaultModel = request.Model,
                SupportsEmbedding = false
            };

            provider = new OpenAiCompatibleProvider(
                request.ProviderType,
                option,
                _httpClientFactory.CreateClient("AiPlatform"),
                _providerLogger);
        }
        catch (Exception ex)
        {
            initError = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(initError) || provider is null)
        {
            yield return new ModelConfigPromptTestStreamEvent("error", initError ?? "Provider 初始化失败");
            yield break;
        }

        var toolDefinitions = request.EnableTools
            ? new[]
            {
                new ChatToolDefinition(
                    "get_current_time",
                    "Get current UTC time in ISO-8601 format.",
                    "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}")
            }
            : null;

        var llmRequest = new ChatCompletionRequest(
            request.Model,
            [new Atlas.Application.AiPlatform.Models.ChatMessage("user", request.Prompt)],
            Temperature: 0.2f,
            MaxTokens: 2048,
            Tools: toolDefinitions,
            ToolChoice: request.EnableTools ? "auto" : null);

        var reasoningSplitter = new ReasoningStreamSplitter();
        string? streamError = null;

        await using var streamEnumerator = provider.ChatStreamAsync(llmRequest, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            ChatCompletionChunk chunk;
            try
            {
                if (!await streamEnumerator.MoveNextAsync())
                {
                    break;
                }
                chunk = streamEnumerator.Current;
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                streamError = ex.Message;
                break;
            }

            if (request.EnableTools && chunk.ToolCalls is { Count: > 0 })
            {
                var toolJson = JsonSerializer.Serialize(chunk.ToolCalls);
                yield return new ModelConfigPromptTestStreamEvent("tool", toolJson);
            }

            if (string.IsNullOrEmpty(chunk.ContentDelta))
            {
                continue;
            }

            foreach (var evt in reasoningSplitter.Append(chunk.ContentDelta, request.EnableReasoning))
            {
                yield return evt;
            }
        }

        foreach (var evt in reasoningSplitter.Flush(request.EnableReasoning))
        {
            yield return evt;
        }

        if (!string.IsNullOrWhiteSpace(streamError))
        {
            yield return new ModelConfigPromptTestStreamEvent("error", streamError);
        }
    }

    private async Task<string> ResolveEffectiveApiKeyAsync(
        TenantId tenantId,
        long? modelConfigId,
        string? incomingApiKey,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(incomingApiKey))
        {
            return incomingApiKey;
        }

        if (!modelConfigId.HasValue)
        {
            throw new BusinessException("ApiKey 不能为空。", ErrorCodes.ValidationError);
        }

        var entity = await _repository.FindByIdAsync(tenantId, modelConfigId.Value, cancellationToken)
            ?? throw new BusinessException("模型配置不存在。", ErrorCodes.NotFound);
        if (string.IsNullOrWhiteSpace(entity.ApiKey))
        {
            throw new BusinessException("已保存的 ApiKey 为空，请先填写并保存。", ErrorCodes.ValidationError);
        }

        return entity.ApiKey;
    }

    private sealed class ReasoningStreamSplitter
    {
        private readonly StringBuilder _buffer = new();
        private bool _insideThink;
        private const string ThinkOpenTag = "<think>";
        private const string ThinkCloseTag = "</think>";

        public IReadOnlyList<ModelConfigPromptTestStreamEvent> Append(string delta, bool enableReasoning)
        {
            if (!enableReasoning)
            {
                return [new ModelConfigPromptTestStreamEvent("final", delta)];
            }

            _buffer.Append(delta);
            var events = new List<ModelConfigPromptTestStreamEvent>();

            while (_buffer.Length > 0)
            {
                var snapshot = _buffer.ToString();
                if (!_insideThink)
                {
                    var openIndex = snapshot.IndexOf(ThinkOpenTag, StringComparison.OrdinalIgnoreCase);
                    if (openIndex < 0)
                    {
                        events.Add(new ModelConfigPromptTestStreamEvent("final", snapshot));
                        _buffer.Clear();
                        break;
                    }

                    if (openIndex > 0)
                    {
                        events.Add(new ModelConfigPromptTestStreamEvent("final", snapshot[..openIndex]));
                    }

                    _buffer.Remove(0, openIndex + ThinkOpenTag.Length);
                    _insideThink = true;
                }
                else
                {
                    var closeIndex = snapshot.IndexOf(ThinkCloseTag, StringComparison.OrdinalIgnoreCase);
                    if (closeIndex < 0)
                    {
                        events.Add(new ModelConfigPromptTestStreamEvent("thought", snapshot));
                        _buffer.Clear();
                        break;
                    }

                    if (closeIndex > 0)
                    {
                        events.Add(new ModelConfigPromptTestStreamEvent("thought", snapshot[..closeIndex]));
                    }

                    _buffer.Remove(0, closeIndex + ThinkCloseTag.Length);
                    _insideThink = false;
                }
            }

            return events;
        }

        public IReadOnlyList<ModelConfigPromptTestStreamEvent> Flush(bool enableReasoning)
        {
            if (_buffer.Length == 0)
            {
                return Array.Empty<ModelConfigPromptTestStreamEvent>();
            }

            var rest = _buffer.ToString();
            _buffer.Clear();

            if (!enableReasoning)
            {
                return [new ModelConfigPromptTestStreamEvent("final", rest)];
            }

            return _insideThink
                ? [new ModelConfigPromptTestStreamEvent("thought", rest)]
                : [new ModelConfigPromptTestStreamEvent("final", rest)];
        }
    }
}
