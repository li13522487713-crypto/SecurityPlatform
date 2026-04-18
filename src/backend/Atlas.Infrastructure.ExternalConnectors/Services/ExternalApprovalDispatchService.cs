using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 默认实现：根据 ApprovalFlowDefinitionId 找到 ExternalApprovalTemplateMapping，
/// 按 IntegrationMode 决定是否调用外部 provider 提单 / 同步状态 / 写 ExternalApprovalInstanceLink。
/// </summary>
public sealed class ExternalApprovalDispatchService : IExternalApprovalDispatchService
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalApprovalTemplateMappingRepository _mappingRepository;
    private readonly IExternalApprovalInstanceLinkRepository _linkRepository;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExternalApprovalDispatchService> _logger;

    public ExternalApprovalDispatchService(
        IConnectorRegistry registry,
        IExternalApprovalTemplateMappingRepository mappingRepository,
        IExternalApprovalInstanceLinkRepository linkRepository,
        IExternalIdentityProviderRepository providerRepository,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGenerator,
        TimeProvider timeProvider,
        ILogger<ExternalApprovalDispatchService> logger)
    {
        _registry = registry;
        _mappingRepository = mappingRepository;
        _linkRepository = linkRepository;
        _providerRepository = providerRepository;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ExternalApprovalDispatchResult> OnInstanceStartedAsync(long localInstanceId, long flowDefinitionId, ExternalApprovalSubmission payload, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var mapping = await FindMappingAsync(tenantId, flowDefinitionId, cancellationToken).ConfigureAwait(false);
        if (mapping is null)
        {
            return new ExternalApprovalDispatchResult { Pushed = false, Reason = "no-mapping" };
        }

        if (mapping.IntegrationMode == IntegrationMode.LocalLed)
        {
            return new ExternalApprovalDispatchResult { Pushed = false, Reason = "local-led" };
        }
        if (!mapping.Enabled)
        {
            return new ExternalApprovalDispatchResult { Pushed = false, Reason = "mapping-disabled" };
        }

        var provider = await _providerRepository.GetByIdAsync(tenantId, mapping.ProviderId, cancellationToken).ConfigureAwait(false);
        if (provider is null || !provider.Enabled)
        {
            return new ExternalApprovalDispatchResult { Pushed = false, Reason = "provider-missing" };
        }

        var providerType = provider.ProviderType.ToProviderType();
        var approvalProvider = _registry.GetApproval(providerType);
        var ctx = new ConnectorContext { TenantId = tenantId.Value, ProviderInstanceId = provider.Id, ProviderType = providerType };

        ExternalApprovalInstanceRef instance;
        try
        {
            instance = await approvalProvider.SubmitApprovalAsync(ctx, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (ConnectorException ex)
        {
            _logger.LogError(ex, "External approval submit failed (provider={ProviderId}).", provider.Id);
            return new ExternalApprovalDispatchResult { Pushed = false, ProviderType = providerType, ProviderId = provider.Id, Reason = ex.Code };
        }

        var link = new ExternalApprovalInstanceLink(
            tenantId,
            _idGenerator.NextId(),
            provider.Id,
            localInstanceId,
            instance.ExternalInstanceId,
            instance.ExternalTemplateId ?? mapping.ExternalTemplateId,
            mapping.IntegrationMode,
            _timeProvider.GetUtcNow());
        await _linkRepository.AddAsync(link, cancellationToken).ConfigureAwait(false);

        return new ExternalApprovalDispatchResult
        {
            Pushed = true,
            ExternalInstanceId = instance.ExternalInstanceId,
            ProviderType = providerType,
            ProviderId = provider.Id,
        };
    }

    public async Task OnInstanceStatusChangedAsync(long localInstanceId, ExternalApprovalStatus newStatus, string? commentText, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        // 检查所有 provider 上的 link（一个 local instance 可能被推到多家 provider，做对账时统一处理）。
        var providers = await _providerRepository.ListAsync(tenantId, type: null, includeDisabled: false, cancellationToken).ConfigureAwait(false);
        foreach (var provider in providers)
        {
            var link = await _linkRepository.GetByLocalAsync(tenantId, provider.Id, localInstanceId, cancellationToken).ConfigureAwait(false);
            if (link is null) continue;

            var providerType = provider.ProviderType.ToProviderType();
            var approval = _registry.GetApproval(providerType);
            var ctx = new ConnectorContext { TenantId = tenantId.Value, ProviderInstanceId = provider.Id, ProviderType = providerType };

            try
            {
                await approval.SyncThirdPartyInstanceAsync(ctx, new ExternalThirdPartyInstancePatch
                {
                    ExternalInstanceId = link.ExternalInstanceId,
                    NewStatus = newStatus,
                    CommentText = commentText,
                    OccurredAt = _timeProvider.GetUtcNow(),
                }, cancellationToken).ConfigureAwait(false);

                link.RecordExternalStatus(newStatus.ToString(), _timeProvider.GetUtcNow());
                await _linkRepository.UpdateAsync(link, cancellationToken).ConfigureAwait(false);
            }
            catch (ConnectorException ex)
            {
                _logger.LogWarning(ex, "External approval sync failed for provider {ProviderId}, local instance {LocalInstanceId}.", provider.Id, localInstanceId);
            }
        }
    }

    private async Task<ExternalApprovalTemplateMapping?> FindMappingAsync(TenantId tenantId, long flowDefinitionId, CancellationToken cancellationToken)
    {
        var providers = await _providerRepository.ListAsync(tenantId, type: null, includeDisabled: false, cancellationToken).ConfigureAwait(false);
        foreach (var provider in providers)
        {
            var mapping = await _mappingRepository.GetByFlowAsync(tenantId, provider.Id, flowDefinitionId, cancellationToken).ConfigureAwait(false);
            if (mapping is not null && mapping.Enabled)
            {
                return mapping;
            }
        }
        return null;
    }
}
