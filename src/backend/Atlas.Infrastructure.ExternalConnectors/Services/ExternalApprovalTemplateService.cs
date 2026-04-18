using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

public sealed class ExternalApprovalTemplateService : IExternalApprovalTemplateService
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly IExternalApprovalTemplateCacheRepository _cacheRepository;
    private readonly IExternalApprovalTemplateMappingRepository _mappingRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ExternalApprovalTemplateService(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalApprovalTemplateCacheRepository cacheRepository,
        IExternalApprovalTemplateMappingRepository mappingRepository,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGenerator,
        TimeProvider timeProvider)
    {
        _registry = registry;
        _providerRepository = providerRepository;
        _cacheRepository = cacheRepository;
        _mappingRepository = mappingRepository;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ExternalApprovalTemplateResponse>> ListCachedAsync(long providerId, CancellationToken cancellationToken)
    {
        var items = await _cacheRepository.ListByProviderAsync(_tenantProvider.GetTenantId(), providerId, cancellationToken).ConfigureAwait(false);
        return items.Select(MapCache).ToList();
    }

    public async Task<ExternalApprovalTemplateResponse> RefreshAsync(long providerId, string externalTemplateId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var provider = await _providerRepository.GetByIdAsync(tenantId, providerId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {providerId} not found.");

        var providerType = provider.ProviderType.ToProviderType();
        var approvalProvider = _registry.GetApproval(providerType);
        var ctx = new ConnectorContext { TenantId = tenantId.Value, ProviderInstanceId = provider.Id, ProviderType = providerType };
        var template = await approvalProvider.GetTemplateAsync(ctx, externalTemplateId, cancellationToken).ConfigureAwait(false);

        var controlsJson = JsonSerializer.Serialize(template.Controls);
        var entity = new ExternalApprovalTemplateCache(
            tenantId,
            _idGenerator.NextId(),
            provider.Id,
            template.ExternalTemplateId,
            template.Name,
            template.Description,
            controlsJson,
            template.RawJson,
            _timeProvider.GetUtcNow());
        await _cacheRepository.UpsertAsync(entity, cancellationToken).ConfigureAwait(false);
        return MapCache(entity);
    }

    public async Task<ExternalApprovalTemplateMappingResponse?> GetMappingAsync(long providerId, long flowDefinitionId, CancellationToken cancellationToken)
    {
        var entity = await _mappingRepository.GetByFlowAsync(_tenantProvider.GetTenantId(), providerId, flowDefinitionId, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : MapMapping(entity);
    }

    public async Task<IReadOnlyList<ExternalApprovalTemplateMappingResponse>> ListMappingsAsync(long providerId, CancellationToken cancellationToken)
    {
        var items = await _mappingRepository.ListByProviderAsync(_tenantProvider.GetTenantId(), providerId, cancellationToken).ConfigureAwait(false);
        return items.Select(MapMapping).ToList();
    }

    public async Task<ExternalApprovalTemplateMappingResponse> UpsertMappingAsync(ExternalApprovalTemplateMappingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMapping(request);
        var tenantId = _tenantProvider.GetTenantId();
        var existing = await _mappingRepository.GetByFlowAsync(tenantId, request.ProviderId, request.FlowDefinitionId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        if (existing is null)
        {
            existing = new ExternalApprovalTemplateMapping(
                tenantId,
                _idGenerator.NextId(),
                request.ProviderId,
                request.FlowDefinitionId,
                request.ExternalTemplateId,
                request.IntegrationMode,
                request.FieldMappingJson,
                request.Enabled,
                now);
            await _mappingRepository.AddAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.Update(request.IntegrationMode, request.FieldMappingJson, now);
            existing.Toggle(request.Enabled, now);
            await _mappingRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        return MapMapping(existing);
    }

    public async Task DeleteMappingAsync(long mappingId, CancellationToken cancellationToken)
        => await _mappingRepository.DeleteAsync(_tenantProvider.GetTenantId(), mappingId, cancellationToken).ConfigureAwait(false);

    private static void ValidateMapping(ExternalApprovalTemplateMappingRequest request)
    {
        if (request.ProviderId <= 0)
        {
            throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", "providerId is required.");
        }
        if (request.FlowDefinitionId <= 0)
        {
            throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", "flowDefinitionId is required.");
        }
        if (string.IsNullOrWhiteSpace(request.ExternalTemplateId))
        {
            throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", "externalTemplateId is required.");
        }

        // 解析 JSON 数组并校验：每条都必须有 localFieldKey + externalControlId + valueType
        try
        {
            using var doc = JsonDocument.Parse(request.FieldMappingJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", "fieldMappingJson must be a JSON array.");
            }
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("localFieldKey", out var local) || local.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(local.GetString()))
                {
                    throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", "Every mapping entry must have non-empty localFieldKey.");
                }
                if (!item.TryGetProperty("externalControlId", out var external) || external.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(external.GetString()))
                {
                    throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", "Every mapping entry must have non-empty externalControlId.");
                }
                if (!item.TryGetProperty("valueType", out var valueType) || valueType.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(valueType.GetString()))
                {
                    throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", "Every mapping entry must have non-empty valueType.");
                }
            }
        }
        catch (JsonException ex)
        {
            throw new BusinessException("CONNECTOR_TEMPLATE_MAPPING_INVALID", $"fieldMappingJson is not valid JSON: {ex.Message}");
        }
    }

    private static ExternalApprovalTemplateResponse MapCache(ExternalApprovalTemplateCache entity)
    {
        IReadOnlyList<ExternalApprovalTemplateControlDto> controls;
        try
        {
            controls = JsonSerializer.Deserialize<List<ExternalApprovalTemplateControlDto>>(entity.ControlsJson) ?? new();
        }
        catch (JsonException)
        {
            controls = Array.Empty<ExternalApprovalTemplateControlDto>();
        }
        return new ExternalApprovalTemplateResponse
        {
            ExternalTemplateId = entity.ExternalTemplateId,
            Name = entity.Name,
            Description = entity.Description,
            Controls = controls,
            FetchedAt = entity.FetchedAt,
        };
    }

    private static ExternalApprovalTemplateMappingResponse MapMapping(ExternalApprovalTemplateMapping entity) => new()
    {
        Id = entity.Id,
        ProviderId = entity.ProviderId,
        FlowDefinitionId = entity.FlowDefinitionId,
        ExternalTemplateId = entity.ExternalTemplateId,
        IntegrationMode = entity.IntegrationMode,
        FieldMappingJson = entity.FieldMappingJson,
        Enabled = entity.Enabled,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
