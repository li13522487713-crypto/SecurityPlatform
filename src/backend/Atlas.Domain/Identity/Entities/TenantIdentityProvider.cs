using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 治理 M-G07-C2（S13）：租户级身份提供方（IdP）配置。
/// 支持 OIDC 与 SAML 两种类型；敏感字段（ClientSecret / SAML 私钥等）经 LowCodeCredentialProtector 加密落库。
/// </summary>
[SugarTable("TenantIdentityProvider")]
public sealed class TenantIdentityProvider : TenantEntity
{
    public const string TypeOidc = "oidc";
    public const string TypeSaml = "saml";

    public TenantIdentityProvider()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        DisplayName = string.Empty;
        IdpType = TypeOidc;
        Enabled = false;
        ConfigJson = "{}";
        SecretJson = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public TenantIdentityProvider(
        TenantId tenantId,
        string code,
        string displayName,
        string idpType,
        bool enabled,
        string configJson,
        string secretJsonEncrypted,
        long createdBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        Code = code.Trim();
        DisplayName = displayName.Trim();
        IdpType = idpType.Trim().ToLowerInvariant();
        Enabled = enabled;
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        SecretJson = secretJsonEncrypted ?? string.Empty;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedBy = createdBy;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Code { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string DisplayName { get; private set; }

    [SugarColumn(Length = 16, IsNullable = false)]
    public string IdpType { get; private set; }

    public bool Enabled { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string ConfigJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string SecretJson { get; private set; }

    public long CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string displayName, bool enabled, string configJson, string? secretJsonEncrypted, long updatedBy)
    {
        DisplayName = displayName.Trim();
        Enabled = enabled;
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        if (!string.IsNullOrEmpty(secretJsonEncrypted))
        {
            SecretJson = secretJsonEncrypted!;
        }
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
