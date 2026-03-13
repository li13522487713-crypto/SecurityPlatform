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

        entity.Update(
            request.Name,
            request.ApiKey,
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
        ModelConfigTestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var option = new AiProviderOption
            {
                ApiKey = request.ApiKey,
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
                    [new ChatMessage("user", "hi")],
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
}
