using Atlas.Connectors.Core.Options;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// Connectors.DingTalk 全局默认 Options。所有值可通过 appsettings 的
/// ExternalConnectors:DingTalk 节覆盖，提供实例运行时的再由 IConnectorRuntimeOptionsResolver 注入。
/// </summary>
public sealed class DingTalkOptions : ConnectorOptions
{
    public override string ProviderType => DingTalkConnectorMarker.ProviderType;

    /// <summary>v1.0 新版 OpenAPI 网关。</summary>
    public string ApiBaseUrl { get; set; } = "https://api.dingtalk.com";

    /// <summary>v1 老版 OpenAPI 网关（oapi.dingtalk.com），部分接口仍只存在于此。</summary>
    public string LegacyApiBaseUrl { get; set; } = "https://oapi.dingtalk.com";

    /// <summary>OAuth2 授权端基址（钉钉身份凭证/扫码登录）。</summary>
    public string OAuthBaseUrl { get; set; } = "https://login.dingtalk.com";

    /// <summary>access_token 实际 7200s，缓存 ttl 留出 5 分钟安全垫。</summary>
    public int AccessTokenSafetyMarginSeconds { get; set; } = 300;

    /// <summary>默认请求 user_id 类型（兼容 unionid/userid/openId）。</summary>
    public string DefaultUserIdType { get; set; } = "userid";
}
