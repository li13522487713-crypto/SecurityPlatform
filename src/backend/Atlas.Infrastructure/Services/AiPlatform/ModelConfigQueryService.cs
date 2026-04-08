using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ModelConfigQueryService : IModelConfigQueryService
{
    private readonly ModelConfigRepository _repository;

    public ModelConfigQueryService(ModelConfigRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ModelConfigDto>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        var dtos = items.Select(Map).ToList();
        return new PagedResult<ModelConfigDto>(dtos, total, pageIndex, pageSize);
    }

    public async Task<ModelConfigDto?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var item = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyList<ModelConfigDto>> GetAllEnabledAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllEnabledAsync(tenantId, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<ModelConfigStatsDto> GetStatsAsync(
        TenantId tenantId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var (total, enabled, embeddingCount) = await _repository.GetStatsAsync(tenantId, keyword, cancellationToken);
        var disabled = total - enabled;
        return new ModelConfigStatsDto(total, enabled, disabled, embeddingCount);
    }

    private static ModelConfigDto Map(ModelConfig item)
        => new(
            item.Id,
            item.Name,
            item.ProviderType,
            item.BaseUrl,
            item.DefaultModel,
            item.ModelId,
            item.SystemPrompt,
            item.IsEnabled,
            item.SupportsEmbedding,
            item.EnableStreaming,
            item.EnableReasoning,
            item.EnableTools,
            item.EnableVision,
            item.EnableJsonMode,
            item.Temperature,
            item.MaxTokens,
            item.TopP,
            item.FrequencyPenalty,
            item.PresencePenalty,
            MaskApiKey(item.ApiKey),
            item.CreatedAt);

    private static string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        if (apiKey.Length <= 8)
        {
            return "********";
        }

        return $"{apiKey[..4]}****{apiKey[^4..]}";
    }
}
