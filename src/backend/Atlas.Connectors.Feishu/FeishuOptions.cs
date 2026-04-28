using Atlas.Connectors.Core.Options;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// Connectors.Feishu 全局默认 Options。
/// </summary>
public sealed class FeishuOptions : ConnectorOptions
{
    public override string ProviderType => FeishuConnectorMarker.ProviderType;

    public string ApiBaseUrl { get; set; } = "https://open.feishu.cn";

    /// <summary>授权端默认 scope；按需透传给 OAuth start。</summary>
    public string OAuthScope { get; set; } = "";

    /// <summary>tenant_access_token 实际有效期 7200s，缓存 ttl 留出 300s 安全垫。</summary>
    public int TenantTokenSafetyMarginSeconds { get; set; } = 300;

    /// <summary>user_access_token 通常 7200s，缓存 ttl 留出 600s 安全垫。</summary>
    public int UserTokenSafetyMarginSeconds { get; set; } = 600;

    /// <summary>
    /// 飞书内置/自建应用 user_id 类型常量：默认请求 open_id，便于跨应用稳定。
    /// 调用 contact API 时若需 union_id / user_id，请在调用处显式覆盖。
    /// </summary>
    public string DefaultUserIdType { get; set; } = "open_id";
}
