using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.DingTalk;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 把 ConnectorContext 解析成 DingTalkRuntimeOptions。
/// SecretEncrypted 中保存的 DingTalkSecretPayload JSON：{ AppSecret, CallbackToken, CallbackAesKey }。
/// </summary>
public sealed class DingTalkRuntimeOptionsResolver : IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions>
{
    private readonly IExternalIdentityProviderRepository _repository;
    private readonly ISecretProtector _secretProtector;

    public DingTalkRuntimeOptionsResolver(IExternalIdentityProviderRepository repository, ISecretProtector secretProtector)
    {
        _repository = repository;
        _secretProtector = secretProtector;
    }

    public async Task<DingTalkRuntimeOptions> ResolveAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(context.TenantId, context.ProviderInstanceId, cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.ProviderNotFound, $"DingTalk provider {context.ProviderInstanceId} not found in tenant {context.TenantId:D}.", DingTalkConnectorMarker.ProviderType);

        if (entity.ProviderType != ConnectorProviderType.DingTalk)
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, $"Provider {entity.Id} is not a DingTalk provider (actual {entity.ProviderType}).", DingTalkConnectorMarker.ProviderType);
        }
        if (!entity.Enabled)
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderDisabled, $"DingTalk provider {entity.Id} is disabled.", DingTalkConnectorMarker.ProviderType);
        }

        var plain = _secretProtector.Decrypt(entity.SecretEncrypted);
        DingTalkSecretPayload? secret = null;
        if (!string.IsNullOrWhiteSpace(plain))
        {
            try
            {
                secret = JsonSerializer.Deserialize<DingTalkSecretPayload>(plain);
            }
            catch (JsonException ex)
            {
                throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "DingTalk secret JSON malformed.", DingTalkConnectorMarker.ProviderType, innerException: ex);
            }
        }

        var appSecret = secret?.AppSecret ?? plain;
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "DingTalk AppSecret missing.", DingTalkConnectorMarker.ProviderType);
        }

        var trustedDomains = string.IsNullOrWhiteSpace(entity.TrustedDomains)
            ? Array.Empty<string>()
            : entity.TrustedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new DingTalkRuntimeOptions
        {
            AppKey = entity.AppId ?? entity.ProviderTenantId,
            AppSecret = appSecret,
            CorpId = entity.ProviderTenantId,
            AgentId = entity.AgentId,
            CallbackBaseUrl = entity.CallbackBaseUrl,
            TrustedDomains = trustedDomains,
            CallbackAesKey = secret?.CallbackAesKey,
            CallbackToken = secret?.CallbackToken,
        };
    }

    private sealed record DingTalkSecretPayload
    {
        public string? AppSecret { get; init; }
        public string? CallbackToken { get; init; }
        public string? CallbackAesKey { get; init; }
    }
}
