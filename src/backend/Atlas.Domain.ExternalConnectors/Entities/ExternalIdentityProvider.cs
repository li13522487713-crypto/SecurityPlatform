using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 外部协同连接器的"实例配置"。一个租户可以注册多个 provider 实例（例如同时连接两套企微 corp）。
/// 凭据字段 <see cref="SecretEncrypted"/> 必须经 DataProtectionService 加密后入库。
/// </summary>
public sealed class ExternalIdentityProvider : TenantEntity
{
    public ExternalIdentityProvider()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        DisplayName = string.Empty;
        ProviderTenantId = string.Empty;
        AppId = string.Empty;
        SecretEncrypted = string.Empty;
        TrustedDomains = string.Empty;
        CallbackBaseUrl = string.Empty;
    }

    public ExternalIdentityProvider(
        TenantId tenantId,
        long id,
        ConnectorProviderType providerType,
        string code,
        string displayName,
        string providerTenantId,
        string appId,
        string secretEncrypted,
        string trustedDomains,
        string callbackBaseUrl,
        string? agentId,
        string? visibilityScope,
        string? syncCron,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderType = providerType;
        Code = code;
        DisplayName = displayName;
        ProviderTenantId = providerTenantId;
        AppId = appId;
        SecretEncrypted = secretEncrypted;
        TrustedDomains = trustedDomains;
        CallbackBaseUrl = callbackBaseUrl;
        AgentId = agentId;
        VisibilityScope = visibilityScope;
        SyncCron = syncCron;
        Enabled = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>provider 类型（wecom / feishu / dingtalk / custom_oidc）。</summary>
    public ConnectorProviderType ProviderType { get; private set; }

    /// <summary>租户内的唯一标识码（同一租户下不可重复，便于前端直接引用）。</summary>
    public string Code { get; private set; }

    /// <summary>展示名（例如"集团企微"）。</summary>
    public string DisplayName { get; private set; }

    /// <summary>外部平台租户主键（企微 corp_id / 飞书 tenant_key / 钉钉 corp_id）。</summary>
    public string ProviderTenantId { get; private set; }

    /// <summary>应用 ID（企微 agent_id 同时存到 AgentId；飞书 app_id；钉钉 app_key）。</summary>
    public string AppId { get; private set; }

    /// <summary>加密后的凭据 JSON：可包含 secret / encoding_aes_key / verification_token 等。</summary>
    public string SecretEncrypted { get; private set; }

    /// <summary>可信跳转域名（逗号分隔），登录回调与 OAuth 起跳必须命中其一，否则拒绝。</summary>
    public string TrustedDomains { get; private set; }

    /// <summary>本平台对外暴露的回调根地址（常用于 OAuth redirect_uri 与 webhook receiver）。</summary>
    public string CallbackBaseUrl { get; private set; }

    /// <summary>企微等需要 agent_id 的 provider 在此填入。</summary>
    public string? AgentId { get; private set; }

    /// <summary>应用的可见范围标签或部门 ID 列表（JSON 字符串），仅作为审计与展示，不参与业务校验。</summary>
    public string? VisibilityScope { get; private set; }

    /// <summary>定时同步表达式（cron），为空时只支持手动同步。</summary>
    public string? SyncCron { get; private set; }

    public bool Enabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateProfile(
        string displayName,
        string providerTenantId,
        string appId,
        string trustedDomains,
        string callbackBaseUrl,
        string? agentId,
        string? visibilityScope,
        string? syncCron,
        DateTimeOffset now)
    {
        DisplayName = displayName;
        ProviderTenantId = providerTenantId;
        AppId = appId;
        TrustedDomains = trustedDomains;
        CallbackBaseUrl = callbackBaseUrl;
        AgentId = agentId;
        VisibilityScope = visibilityScope;
        SyncCron = syncCron;
        UpdatedAt = now;
    }

    public void RotateSecret(string newSecretEncrypted, DateTimeOffset now)
    {
        SecretEncrypted = newSecretEncrypted;
        UpdatedAt = now;
    }

    public void Enable(DateTimeOffset now)
    {
        Enabled = true;
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        Enabled = false;
        UpdatedAt = now;
    }

    public bool IsTrustedDomain(string host)
    {
        if (string.IsNullOrWhiteSpace(TrustedDomains))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        foreach (var raw in TrustedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(raw, host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
