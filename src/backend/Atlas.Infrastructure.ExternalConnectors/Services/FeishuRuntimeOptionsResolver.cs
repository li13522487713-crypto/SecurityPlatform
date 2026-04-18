using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Feishu;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

public sealed class FeishuRuntimeOptionsResolver : IConnectorRuntimeOptionsResolver<FeishuRuntimeOptions>
{
    private readonly IExternalIdentityProviderRepository _repository;
    private readonly ISecretProtector _secretProtector;

    public FeishuRuntimeOptionsResolver(IExternalIdentityProviderRepository repository, ISecretProtector secretProtector)
    {
        _repository = repository;
        _secretProtector = secretProtector;
    }

    public async Task<FeishuRuntimeOptions> ResolveAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(context.TenantId, context.ProviderInstanceId, cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.ProviderNotFound, $"Feishu provider {context.ProviderInstanceId} not found in tenant {context.TenantId:D}.", FeishuConnectorMarker.ProviderType);

        if (entity.ProviderType != ConnectorProviderType.Feishu)
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, $"Provider {entity.Id} is not a Feishu provider (actual {entity.ProviderType}).", FeishuConnectorMarker.ProviderType);
        }
        if (!entity.Enabled)
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderDisabled, $"Feishu provider {entity.Id} is disabled.", FeishuConnectorMarker.ProviderType);
        }

        var plain = _secretProtector.Decrypt(entity.SecretEncrypted);
        FeishuSecretPayload? secret = null;
        if (!string.IsNullOrWhiteSpace(plain))
        {
            try
            {
                secret = JsonSerializer.Deserialize<FeishuSecretPayload>(plain);
            }
            catch (JsonException ex)
            {
                throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "Feishu secret JSON malformed.", FeishuConnectorMarker.ProviderType, innerException: ex);
            }
        }

        var appSecret = secret?.AppSecret ?? plain;
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "Feishu app_secret missing.", FeishuConnectorMarker.ProviderType);
        }

        var trustedDomains = string.IsNullOrWhiteSpace(entity.TrustedDomains)
            ? Array.Empty<string>()
            : entity.TrustedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new FeishuRuntimeOptions
        {
            AppId = entity.AppId,
            AppSecret = appSecret,
            TenantKey = entity.ProviderTenantId,
            CallbackBaseUrl = entity.CallbackBaseUrl,
            TrustedDomains = trustedDomains,
            EventVerificationToken = secret?.EventVerificationToken,
            EventEncryptKey = secret?.EventEncryptKey,
        };
    }

    private sealed record FeishuSecretPayload
    {
        public string? AppSecret { get; init; }
        public string? EventVerificationToken { get; init; }
        public string? EventEncryptKey { get; init; }
    }
}
