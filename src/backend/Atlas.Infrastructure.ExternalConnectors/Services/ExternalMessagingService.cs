using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 外部消息派发 facade：所有走 REST 的发卡 / 发消息都先经过此服务，
/// 保证 ExternalMessageDispatch 表持续记录"何时发了什么、外部 messageId 是什么、卡片版本是几"。
/// 卡片更新链路依赖该表回查 ResponseCode（企微 update_template_card 必需）。
/// </summary>
public sealed class ExternalMessagingService : IExternalMessagingService
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly IExternalMessageDispatchRepository _dispatchRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExternalMessagingService> _logger;

    public ExternalMessagingService(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalMessageDispatchRepository dispatchRepository,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGenerator,
        TimeProvider timeProvider,
        ILogger<ExternalMessagingService> logger)
    {
        _registry = registry;
        _providerRepository = providerRepository;
        _dispatchRepository = dispatchRepository;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ExternalMessageDispatchSummary> SendAsync(SendExternalMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _tenantProvider.GetTenantId();
        var provider = await _providerRepository.GetByIdAsync(tenantId, request.ProviderId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {request.ProviderId} not found.");
        if (!provider.Enabled)
        {
            throw new BusinessException("CONNECTOR_PROVIDER_DISABLED", $"Provider {request.ProviderId} is disabled.");
        }

        var providerType = provider.ProviderType.ToProviderType();
        var messaging = _registry.GetMessaging(providerType);
        var ctx = new ConnectorContext { TenantId = tenantId.Value, ProviderInstanceId = provider.Id, ProviderType = providerType };

        var entity = new ExternalMessageDispatch(
            tenantId,
            _idGenerator.NextId(),
            provider.Id,
            request.BusinessKey,
            JsonSerializer.Serialize(request.Recipient),
            request.Card is not null ? JsonSerializer.Serialize(request.Card) : (request.Text ?? string.Empty),
            isCard: request.Card is not null,
            _timeProvider.GetUtcNow());
        await _dispatchRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        try
        {
            ExternalMessageDispatchResult result;
            if (request.Card is not null)
            {
                result = await messaging.SendCardAsync(ctx, request.Recipient, request.Card, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = await messaging.SendTextAsync(ctx, request.Recipient, request.Text ?? string.Empty, cancellationToken).ConfigureAwait(false);
            }

            entity.MarkSent(result.MessageId ?? string.Empty, result.ResponseCode, _timeProvider.GetUtcNow());
            await _dispatchRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);

            return new ExternalMessageDispatchSummary
            {
                DispatchId = entity.Id,
                Status = "sent",
                MessageId = entity.MessageId,
                ResponseCode = entity.ResponseCode,
                CardVersion = entity.CardVersion,
                ProviderType = providerType,
            };
        }
        catch (ConnectorException ex)
        {
            entity.MarkFailed(ex.Message, _timeProvider.GetUtcNow());
            await _dispatchRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(ex, "External messaging send failed: provider={ProviderId} businessKey={BusinessKey}.", request.ProviderId, request.BusinessKey);
            return new ExternalMessageDispatchSummary
            {
                DispatchId = entity.Id,
                Status = "failed",
                ProviderType = providerType,
                ErrorMessage = ex.Code,
            };
        }
    }

    public async Task<ExternalMessageDispatchSummary> UpdateCardAsync(long dispatchId, UpdateCardRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _dispatchRepository.GetAsync(tenantId, dispatchId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_DISPATCH_NOT_FOUND", $"Dispatch {dispatchId} not found.");

        var provider = await _providerRepository.GetByIdAsync(tenantId, entity.ProviderId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {entity.ProviderId} not found.");
        var providerType = provider.ProviderType.ToProviderType();
        var messaging = _registry.GetMessaging(providerType);
        var ctx = new ConnectorContext { TenantId = tenantId.Value, ProviderInstanceId = provider.Id, ProviderType = providerType };

        // 用 dispatch entity 的快照重建上一次结果（企微 update_template_card 需要 response_code）
        var previous = new ExternalMessageDispatchResult
        {
            ProviderType = providerType,
            MessageId = entity.MessageId ?? string.Empty,
            ResponseCode = entity.ResponseCode,
            CardVersion = entity.CardVersion,
            RawJson = "{}",
        };

        try
        {
            var updated = await messaging.UpdateCardAsync(ctx, previous, request.Card, cancellationToken).ConfigureAwait(false);
            entity.MarkUpdated(updated.ResponseCode ?? entity.ResponseCode, updated.CardVersion, _timeProvider.GetUtcNow());
            await _dispatchRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            return new ExternalMessageDispatchSummary
            {
                DispatchId = entity.Id,
                Status = "updated",
                MessageId = entity.MessageId,
                ResponseCode = entity.ResponseCode,
                CardVersion = entity.CardVersion,
                ProviderType = providerType,
            };
        }
        catch (ConnectorException ex)
        {
            entity.MarkFailed(ex.Message, _timeProvider.GetUtcNow());
            await _dispatchRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            return new ExternalMessageDispatchSummary
            {
                DispatchId = entity.Id,
                Status = "failed",
                ProviderType = providerType,
                ErrorMessage = ex.Code,
            };
        }
    }
}
