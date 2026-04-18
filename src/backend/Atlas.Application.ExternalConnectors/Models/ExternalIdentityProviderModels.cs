using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Models;

public sealed class ExternalIdentityProviderCreateRequest
{
    public ConnectorProviderType ProviderType { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ProviderTenantId { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;

    /// <summary>明文凭据，按 provider 的字段名打成 JSON。Infrastructure 层落库时统一加密。</summary>
    public string SecretJson { get; set; } = string.Empty;

    public string TrustedDomains { get; set; } = string.Empty;

    public string CallbackBaseUrl { get; set; } = string.Empty;

    public string? AgentId { get; set; }

    public string? VisibilityScope { get; set; }

    public string? SyncCron { get; set; }
}

public sealed class ExternalIdentityProviderUpdateRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string ProviderTenantId { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;

    public string TrustedDomains { get; set; } = string.Empty;

    public string CallbackBaseUrl { get; set; } = string.Empty;

    public string? AgentId { get; set; }

    public string? VisibilityScope { get; set; }

    public string? SyncCron { get; set; }
}

public sealed class ExternalIdentityProviderRotateSecretRequest
{
    public string SecretJson { get; set; } = string.Empty;
}

public sealed class ExternalIdentityProviderResponse
{
    public long Id { get; set; }

    public ConnectorProviderType ProviderType { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ProviderTenantId { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;

    /// <summary>仅返回脱敏（前 4 位 + ****）后的密钥指纹。</summary>
    public string SecretMasked { get; set; } = string.Empty;

    public string TrustedDomains { get; set; } = string.Empty;

    public string CallbackBaseUrl { get; set; } = string.Empty;

    public string? AgentId { get; set; }

    public string? VisibilityScope { get; set; }

    public string? SyncCron { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ExternalIdentityProviderListItem
{
    public long Id { get; set; }

    public ConnectorProviderType ProviderType { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
