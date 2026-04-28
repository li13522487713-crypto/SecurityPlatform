namespace Atlas.Connectors.Core.Options;

/// <summary>
/// 所有外部协同连接器（企微 / 飞书 / 钉钉 / 自建 OIDC）共享的配置基类。
/// 子类按 provider 私有需求扩展（如 CorpId / TenantKey / AppId）。
/// </summary>
public abstract class ConnectorOptions
{
    /// <summary>
    /// 在 ConnectorRegistry 中唯一标识 provider 类型，例如 "wecom" / "feishu" / "dingtalk"。
    /// 子类构造函数固定该值，避免外部配置覆盖造成路由错乱。
    /// </summary>
    public abstract string ProviderType { get; }

    /// <summary>HTTP 客户端默认超时，单位毫秒。</summary>
    public int DefaultRequestTimeoutMs { get; set; } = 15_000;

    /// <summary>access_token / tenant_access_token 缓存 TTL（秒）。</summary>
    public int TokenCacheTtlSeconds { get; set; } = 6_900;

    /// <summary>OAuth state 防 CSRF 票据存活时间（秒）。</summary>
    public int OAuthStateTtlSeconds { get; set; } = 600;

    /// <summary>Webhook 重放窗口（秒），超过窗口的请求视为可疑事件直接拒绝。</summary>
    public int WebhookReplayWindowSeconds { get; set; } = 300;

    /// <summary>是否在调用失败时自动写入审计日志（默认开启）。</summary>
    public bool WriteAuditLogOnFailure { get; set; } = true;
}
