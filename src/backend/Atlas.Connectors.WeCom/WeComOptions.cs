using Atlas.Connectors.Core.Options;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// Connectors.WeCom 的全局默认 Options。
/// 跨实例共享的全局开关（API base / 默认超时 / token 缓存 ttl）放在这里；单个实例的 corp_id / secret 走 WeComRuntimeOptions。
/// </summary>
public sealed class WeComOptions : ConnectorOptions
{
    public override string ProviderType => WeComConnectorMarker.ProviderType;

    public string ApiBaseUrl { get; set; } = "https://qyapi.weixin.qq.com";

    public string OAuthBaseUrl { get; set; } = "https://open.weixin.qq.com";

    /// <summary>OAuth2 授权端 scope（默认 snsapi_base 仅取 userid）。</summary>
    public string OAuthScope { get; set; } = "snsapi_base";

    /// <summary>access_token 缓存 ttl 略小于实际 7200s，避免边界问题（默认 6900s）。</summary>
    public int AccessTokenSafetyMarginSeconds { get; set; } = 300;
}
