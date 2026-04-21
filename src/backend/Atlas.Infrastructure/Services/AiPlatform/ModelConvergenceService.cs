using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ModelConvergenceService : IModelConvergenceService
{
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;

    public ModelConvergenceService(
        ModelConfigRepository modelConfigRepository,
        IOptionsMonitor<AiPlatformOptions> optionsMonitor)
    {
        _modelConfigRepository = modelConfigRepository;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<ModelConvergenceResponse> AnalyzeAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var enabledModels = await _modelConfigRepository.GetAllEnabledAsync(
            tenantId,
            workspaceId: null,
            cancellationToken: cancellationToken);
        var diffs = enabledModels
            .Select(item => new ModelConvergenceDiffItem(
                item.ProviderType,
                item.DefaultModel,
                item.SupportsEmbedding,
                item.EnableStreaming,
                item.EnableTools,
                item.EnableJsonMode,
                item.BaseUrl))
            .OrderBy(item => item.Provider)
            .ThenBy(item => item.ModelName)
            .ToArray();

        var preferred = enabledModels
            .Where(item => item.EnableTools && item.EnableJsonMode)
            .OrderByDescending(item => item.EnableReasoning)
            .ThenBy(item => item.ProviderType)
            .FirstOrDefault()
            ?? enabledModels.FirstOrDefault();

        var embedding = enabledModels
            .Where(item => item.SupportsEmbedding)
            .OrderBy(item => item.ProviderType)
            .FirstOrDefault();

        var options = _optionsMonitor.CurrentValue;
        var profile = new ModelConvergenceProfile(
            RecommendedProvider: preferred?.ProviderType ?? options.DefaultProvider,
            RecommendedModel: preferred?.DefaultModel ?? ResolveDefaultModel(options, options.DefaultProvider),
            EmbeddingProvider: embedding?.ProviderType ?? options.Embedding.Provider,
            EmbeddingModel: embedding?.DefaultModel ?? options.Embedding.Model,
            Reasons:
            [
                "统一采用单一主对话模型，降低路由与调试复杂度。",
                "Embedding 与对话模型解耦，保证检索稳定性与成本可控。",
                "优先选择支持工具调用与 JSON 输出的模型，满足编排场景可控性。"
            ]);

        return new ModelConvergenceResponse(diffs, profile);
    }

    private static string ResolveDefaultModel(AiPlatformOptions options, string providerName)
    {
        if (options.Providers.TryGetValue(providerName, out var provider)
            && !string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel;
        }

        return "gpt-4o-mini";
    }
}
