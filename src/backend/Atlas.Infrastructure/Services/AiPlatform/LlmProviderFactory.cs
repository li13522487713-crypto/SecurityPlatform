using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class LlmProviderFactory : ILlmProviderFactory
{
    private static readonly IReadOnlyDictionary<string, string> DefaultBaseUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] = "https://api.openai.com",
        ["deepseek"] = "https://api.deepseek.com",
        ["ollama"] = "http://localhost:11434"
    };

    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMeteringService _meteringService;
    private readonly ILogger<OpenAiCompatibleProvider> _providerLogger;
    private readonly ISecretRefResolver _secretRefResolver;
    private readonly IAtlasHybridCache _cache;

    public LlmProviderFactory(
        IOptionsMonitor<AiPlatformOptions> options,
        ModelConfigRepository modelConfigRepository,
        ITenantProvider tenantProvider,
        IHttpClientFactory httpClientFactory,
        IMeteringService meteringService,
        IAtlasHybridCache cache,
        ISecretRefResolver secretRefResolver,
        ILogger<OpenAiCompatibleProvider> providerLogger)
    {
        _optionsMonitor = options;
        _modelConfigRepository = modelConfigRepository;
        _tenantProvider = tenantProvider;
        _httpClientFactory = httpClientFactory;
        _meteringService = meteringService;
        _cache = cache;
        _secretRefResolver = secretRefResolver;
        _providerLogger = providerLogger;
    }

    public ILlmProvider GetLlmProvider(string? providerName = null)
    {
        if (IsFixtureProvider(providerName))
        {
            return new FixtureLlmProvider();
        }

        var options = _optionsMonitor.CurrentValue;
        var dbConfig = ResolveModelConfig(providerName, forEmbedding: false, options);
        if (dbConfig is not null)
        {
            return BuildProvider(dbConfig);
        }

        var name = ResolveProviderName(providerName, options.DefaultProvider, options.DefaultProvider);
        return BuildProvider(name, options, requireEmbedding: false);
    }

    public IEmbeddingProvider GetEmbeddingProvider(string? providerName = null)
    {
        var options = _optionsMonitor.CurrentValue;
        var dbConfig = ResolveModelConfig(providerName, forEmbedding: true, options);
        if (dbConfig is not null)
        {
            return BuildProvider(dbConfig);
        }

        var fallback = options.Embedding.Provider;
        var name = ResolveProviderName(providerName, fallback, options.DefaultProvider);
        if (TryBuildProvider(name, options, requireEmbedding: true, out var provider))
        {
            return provider;
        }

        foreach (var item in options.Providers)
        {
            if (!item.Value.SupportsEmbedding)
            {
                continue;
            }

            if (TryBuildProvider(item.Key, options, requireEmbedding: true, out var embeddingProvider))
            {
                return embeddingProvider;
            }
        }

        throw new KeyNotFoundException($"Embedding provider '{name}' is not registered.");
    }

    private ModelConfig? ResolveModelConfig(string? requestedProviderName, bool forEmbedding, AiPlatformOptions options)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            return null;
        }

        var enabledConfigs = _cache.GetOrCreateAsync(
                BuildModelConfigCacheKey(tenantId),
                async ct =>
                {
                    var rows = await _modelConfigRepository.GetAllEnabledAsync(
                        tenantId,
                        workspaceId: null,
                        cancellationToken: ct);
                    return rows.ToArray();
                },
                TimeSpan.FromMinutes(2),
                [BuildModelConfigCacheTag(tenantId)],
                cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (enabledConfigs is null || enabledConfigs.Length == 0)
        {
            return null;
        }

        var matched = MatchModelConfig(enabledConfigs, requestedProviderName, forEmbedding, options);
        return matched;
    }

    private ModelConfig? MatchModelConfig(
        IReadOnlyList<ModelConfig> configs,
        string? requestedProviderName,
        bool forEmbedding,
        AiPlatformOptions options)
    {
        if (!string.IsNullOrWhiteSpace(requestedProviderName))
        {
            var request = requestedProviderName.Trim();
            var exact = configs.FirstOrDefault(x =>
                string.Equals(x.Name, request, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ProviderType, request, StringComparison.OrdinalIgnoreCase));
            if (exact is not null && (!forEmbedding || exact.SupportsEmbedding))
            {
                return exact;
            }

            // 显式指定 provider 时，不应回退到任意启用模型配置，避免请求被错误路由到非预期 provider。
            return null;
        }

        var preferred = ResolveProviderName(requestedProviderName, options.DefaultProvider, options.Embedding.Provider);
        var byPreferredProvider = configs.FirstOrDefault(x =>
            string.Equals(x.ProviderType, preferred, StringComparison.OrdinalIgnoreCase)
            && (!forEmbedding || x.SupportsEmbedding));
        if (byPreferredProvider is not null)
        {
            return byPreferredProvider;
        }

        return configs.FirstOrDefault(x => !forEmbedding || x.SupportsEmbedding);
    }

    private OpenAiCompatibleProvider BuildProvider(ModelConfig config)
    {
        var option = new AiProviderOption
        {
            ApiKey = _secretRefResolver.Resolve(config.ApiKey),
            BaseUrl = config.BaseUrl,
            DefaultModel = config.DefaultModel,
            SupportsEmbedding = config.SupportsEmbedding
        };

        var client = _httpClientFactory.CreateClient("AiPlatform");
        return new OpenAiCompatibleProvider(
            config.ProviderType,
            option,
            client,
            _tenantProvider.GetTenantId(),
            _meteringService,
            _providerLogger);
    }

    private OpenAiCompatibleProvider BuildProvider(string providerName, AiPlatformOptions options, bool requireEmbedding)
    {
        if (!TryBuildProvider(providerName, options, requireEmbedding, out var provider))
        {
            throw new KeyNotFoundException($"AI provider '{providerName}' is not configured.");
        }

        return provider;
    }

    private bool TryBuildProvider(
        string providerName,
        AiPlatformOptions options,
        bool requireEmbedding,
        out OpenAiCompatibleProvider provider)
    {
        provider = default!;
        if (!options.Providers.TryGetValue(providerName, out var configured))
        {
            configured = new AiProviderOption();
        }

        var merged = new AiProviderOption
        {
            ApiKey = _secretRefResolver.Resolve(configured.ApiKey),
            BaseUrl = ResolveBaseUrl(providerName, configured.BaseUrl),
            DefaultModel = configured.DefaultModel,
            SupportsEmbedding = configured.SupportsEmbedding
        };

        if (string.IsNullOrWhiteSpace(merged.BaseUrl))
        {
            return false;
        }

        if (requireEmbedding && !merged.SupportsEmbedding)
        {
            return false;
        }

        var client = _httpClientFactory.CreateClient("AiPlatform");
        provider = new OpenAiCompatibleProvider(
            providerName,
            merged,
            client,
            _tenantProvider.GetTenantId(),
            _meteringService,
            _providerLogger);
        return true;
    }

    private static string ResolveBaseUrl(string providerName, string? configuredBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.Trim();
        }

        return DefaultBaseUrls.TryGetValue(providerName, out var value)
            ? value
            : string.Empty;
    }

    private static string ResolveProviderName(string? explicitProvider, string fallbackProvider, string secondaryFallback)
    {
        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            return explicitProvider.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fallbackProvider))
        {
            return fallbackProvider.Trim();
        }

        if (!string.IsNullOrWhiteSpace(secondaryFallback))
        {
            return secondaryFallback.Trim();
        }

        throw new InvalidOperationException("No AI provider name specified and no default provider configured.");
    }

    private static string BuildModelConfigCacheKey(TenantId tenantId)
        => $"ai:model-configs:{tenantId.Value:N}:enabled";

    private static string BuildModelConfigCacheTag(TenantId tenantId)
        => $"tag:ai:model-configs:{tenantId.Value:N}";

    private static bool IsFixtureProvider(string? providerName)
        => string.Equals(providerName, "fixture", StringComparison.OrdinalIgnoreCase)
           || string.Equals(providerName, "atlas-fixture", StringComparison.OrdinalIgnoreCase)
           || string.Equals(providerName, "mvp-fixture", StringComparison.OrdinalIgnoreCase);

    private sealed class FixtureLlmProvider : ILlmProvider
    {
        public string ProviderName => "atlas-fixture";

        public Task<ChatCompletionResult> ChatAsync(ChatCompletionRequest request, CancellationToken ct = default)
        {
            var prompt = string.Join(
                "\n",
                request.Messages.Select(message => message.Content ?? string.Empty));
            var content = ResolveContent(prompt);
            return Task.FromResult(new ChatCompletionResult(
                content,
                request.Model,
                ProviderName,
                "stop",
                PromptTokens: null,
                CompletionTokens: null,
                TotalTokens: null));
        }

        public async IAsyncEnumerable<ChatCompletionChunk> ChatStreamAsync(ChatCompletionRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new ChatCompletionChunk(ResolveContent(string.Join("\n", request.Messages.Select(message => message.Content ?? string.Empty))), true, "stop");
            await Task.CompletedTask;
        }

        private static string ResolveContent(string prompt)
        {
            if (prompt.Contains("意图候选列表", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("意图分类", StringComparison.OrdinalIgnoreCase))
            {
                var intent = prompt.Contains("报销", StringComparison.OrdinalIgnoreCase)
                    ? "报销"
                    : prompt.Contains("请假", StringComparison.OrdinalIgnoreCase)
                        ? "请假"
                        : "其他";
                return $$"""{"intent":"{{intent}}","confidence":0.99,"reason":"fixture deterministic classification"}""";
            }

            if (prompt.Contains("法国", StringComparison.OrdinalIgnoreCase) &&
                (prompt.Contains("首都", StringComparison.OrdinalIgnoreCase) ||
                 prompt.Contains("capital", StringComparison.OrdinalIgnoreCase)))
            {
                return "巴黎";
            }

            return prompt.Trim();
        }
    }
}
