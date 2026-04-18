using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.WeCom;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 把 ConnectorContext 解析成 WeComRuntimeOptions。
/// 实现：从 ExternalIdentityProviderRepository 拉实体，使用 ISecretProtector 解密 SecretJson 后构造 RuntimeOptions。
/// </summary>
public sealed class WeComRuntimeOptionsResolver : IConnectorRuntimeOptionsResolver<WeComRuntimeOptions>
{
    private readonly IExternalIdentityProviderRepository _repository;
    private readonly ISecretProtector _secretProtector;

    public WeComRuntimeOptionsResolver(IExternalIdentityProviderRepository repository, ISecretProtector secretProtector)
    {
        _repository = repository;
        _secretProtector = secretProtector;
    }

    public async Task<WeComRuntimeOptions> ResolveAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(context.TenantId, context.ProviderInstanceId, cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.ProviderNotFound, $"WeCom provider {context.ProviderInstanceId} not found in tenant {context.TenantId:D}.", WeComConnectorMarker.ProviderType);

        if (entity.ProviderType != ConnectorProviderType.WeCom)
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, $"Provider {entity.Id} is not a WeCom provider (actual {entity.ProviderType}).", WeComConnectorMarker.ProviderType);
        }
        if (!entity.Enabled)
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderDisabled, $"WeCom provider {entity.Id} is disabled.", WeComConnectorMarker.ProviderType);
        }

        var plain = _secretProtector.Decrypt(entity.SecretEncrypted);
        WeComSecretPayload? secret = null;
        if (!string.IsNullOrWhiteSpace(plain))
        {
            try
            {
                secret = JsonSerializer.Deserialize<WeComSecretPayload>(plain);
            }
            catch (JsonException ex)
            {
                throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "WeCom secret JSON malformed.", WeComConnectorMarker.ProviderType, innerException: ex);
            }
        }

        var corpSecret = secret?.CorpSecret ?? plain;
        if (string.IsNullOrWhiteSpace(corpSecret))
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "WeCom corp_secret missing.", WeComConnectorMarker.ProviderType);
        }
        if (string.IsNullOrWhiteSpace(entity.AgentId))
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "WeCom provider requires AgentId.", WeComConnectorMarker.ProviderType);
        }

        var trustedDomains = string.IsNullOrWhiteSpace(entity.TrustedDomains)
            ? Array.Empty<string>()
            : entity.TrustedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new WeComRuntimeOptions
        {
            CorpId = entity.ProviderTenantId,
            CorpSecret = corpSecret,
            AgentId = entity.AgentId!,
            CallbackBaseUrl = entity.CallbackBaseUrl,
            TrustedDomains = trustedDomains,
            CallbackToken = secret?.CallbackToken,
            CallbackEncodingAesKey = secret?.CallbackEncodingAesKey,
        };
    }

    private sealed record WeComSecretPayload
    {
        public string? CorpSecret { get; init; }
        public string? CallbackToken { get; init; }
        public string? CallbackEncodingAesKey { get; init; }
    }
}
