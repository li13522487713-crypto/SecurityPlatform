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
    private readonly IReadOnlyDictionary<string, ILlmProvider> _llmProviders;
    private readonly IReadOnlyDictionary<string, IEmbeddingProvider> _embeddingProviders;
    private readonly AiPlatformOptions _options;
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiCompatibleProvider> _providerLogger;

    public LlmProviderFactory(
        IEnumerable<ILlmProvider> llmProviders,
        IEnumerable<IEmbeddingProvider> embeddingProviders,
        IOptions<AiPlatformOptions> options,
        ModelConfigRepository modelConfigRepository,
        ITenantProvider tenantProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiCompatibleProvider> providerLogger)
    {
        _llmProviders = llmProviders.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _embeddingProviders = embeddingProviders.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _modelConfigRepository = modelConfigRepository;
        _tenantProvider = tenantProvider;
        _httpClientFactory = httpClientFactory;
        _providerLogger = providerLogger;
    }

    public ILlmProvider GetLlmProvider(string? providerName = null)
    {
        var dbConfig = ResolveModelConfig(providerName, forEmbedding: false);
        if (dbConfig is not null)
        {
            return BuildProvider(dbConfig);
        }

        var name = ResolveProviderName(providerName, _options.DefaultProvider, _options.DefaultProvider);
        if (_llmProviders.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"LLM provider '{name}' is not registered.");
    }

    public IEmbeddingProvider GetEmbeddingProvider(string? providerName = null)
    {
        var dbConfig = ResolveModelConfig(providerName, forEmbedding: true);
        if (dbConfig is not null)
        {
            return BuildProvider(dbConfig);
        }

        var fallback = _options.Embedding.Provider;
        var name = ResolveProviderName(providerName, fallback, _options.DefaultProvider);
        if (_embeddingProviders.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"Embedding provider '{name}' is not registered.");
    }

    private ModelConfig? ResolveModelConfig(string? requestedProviderName, bool forEmbedding)
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

        var matched = MatchModelConfig(enabledConfigs, requestedProviderName, forEmbedding);
        return matched;
    }

    private ModelConfig? MatchModelConfig(
        IReadOnlyList<ModelConfig> configs,
        string? requestedProviderName,
        bool forEmbedding)
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

        var preferred = ResolveProviderName(requestedProviderName, _options.DefaultProvider, _options.Embedding.Provider);
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
