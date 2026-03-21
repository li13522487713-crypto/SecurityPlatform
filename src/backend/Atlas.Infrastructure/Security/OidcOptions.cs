namespace Atlas.Infrastructure.Security;

/// <summary>
/// 单个 OIDC/SSO 提供者配置
/// </summary>
public sealed class OidcProviderConfig
{
    /// <summary>提供者唯一标识（URL-safe，如 "github"、"google"、"internal-ldap"）</summary>
    public string ProviderId { get; init; } = string.Empty;
    /// <summary>前端展示名称（如 "GitHub"、"企业微信"）</summary>
    public string DisplayName { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public string Authority { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = ["openid", "profile", "email"];
    /// <summary>OIDC callback path，需全局唯一（如 /auth/sso/github/callback）</summary>
    public string CallbackPath { get; init; } = string.Empty;
    /// <summary>OIDC claim -> Atlas role 映射规则</summary>
    public Dictionary<string, string> RoleClaimMapping { get; init; } = new();
    /// <summary>前端展示的图标 URL（可选）</summary>
    public string IconUrl { get; init; } = string.Empty;
}

/// <summary>
/// OIDC 全局配置选项（支持多 IdP）
/// </summary>
public sealed class OidcOptions
{
    /// <summary>全局开关；为 true 时注册 Providers 中所有 Enabled 的提供者</summary>
    public bool Enabled { get; init; }

    // ── 向后兼容：单 IdP 配置（会被合并为 Id="default" 的 Provider）──
    public string Authority { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = ["openid", "profile", "email"];
    public string CallbackPath { get; init; } = "/auth/oidc/callback";
    public Dictionary<string, string> RoleClaimMapping { get; init; } = new();

    /// <summary>多 IdP 配置列表（推荐方式）</summary>
    public OidcProviderConfig[] Providers { get; init; } = [];

    /// <summary>返回所有启用的提供者（合并向后兼容的单 IdP 设置）</summary>
    public IReadOnlyList<OidcProviderConfig> GetEffectiveProviders()
    {
        var list = new List<OidcProviderConfig>();

        // 优先使用 Providers 列表
        foreach (var p in Providers)
        {
            if (p.Enabled && !string.IsNullOrWhiteSpace(p.Authority) && !string.IsNullOrWhiteSpace(p.ClientId))
            {
                list.Add(p);
            }
        }

        // 向后兼容：若 Providers 为空但有旧式单 IdP 配置，自动转换
        if (list.Count == 0 && !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId))
        {
            list.Add(new OidcProviderConfig
            {
                ProviderId = "default",
                DisplayName = "Single Sign-On",
                Enabled = true,
                Authority = Authority,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                Scopes = Scopes,
                CallbackPath = string.IsNullOrWhiteSpace(CallbackPath) ? "/auth/sso/default/callback" : CallbackPath,
                RoleClaimMapping = RoleClaimMapping
            });
        }

        return list;
    }
}
