using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
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
    private readonly ILogger<OpenAiCompatibleProvider> _providerLogger;

    public LlmProviderFactory(
        IOptionsMonitor<AiPlatformOptions> options,
        ModelConfigRepository modelConfigRepository,
        ITenantProvider tenantProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiCompatibleProvider> providerLogger)
    {
        _optionsMonitor = options;
        _modelConfigRepository = modelConfigRepository;
        _tenantProvider = tenantProvider;
        _httpClientFactory = httpClientFactory;
        _providerLogger = providerLogger;
    }

    public ILlmProvider GetLlmProvider(string? providerName = null)
    {
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

        var enabledConfigs = _modelConfigRepository
            .GetAllEnabledAsync(tenantId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (enabledConfigs.Count == 0)
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
            ApiKey = config.ApiKey,
            BaseUrl = config.BaseUrl,
            DefaultModel = config.DefaultModel,
            SupportsEmbedding = config.SupportsEmbedding
        };

        var client = _httpClientFactory.CreateClient("AiPlatform");
        return new OpenAiCompatibleProvider(config.ProviderType, option, client, _providerLogger);
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
            ApiKey = configured.ApiKey,
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
        provider = new OpenAiCompatibleProvider(providerName, merged, client, _providerLogger);
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
}
