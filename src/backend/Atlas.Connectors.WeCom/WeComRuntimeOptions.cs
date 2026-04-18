namespace Atlas.Connectors.WeCom;

/// <summary>
/// 单个 WeCom provider 实例的运行时配置（已解密）。
/// 由 Infrastructure.ExternalConnectors 的 IConnectorRuntimeOptionsResolver 实现读取 ExternalIdentityProvider 实体后构造。
/// </summary>
public sealed record WeComRuntimeOptions
{
    public required string CorpId { get; init; }

    public required string CorpSecret { get; init; }

    /// <summary>企微应用 agent_id；自建应用必填。</summary>
    public required string AgentId { get; init; }

    public required string CallbackBaseUrl { get; init; }

    public IReadOnlyList<string> TrustedDomains { get; init; } = Array.Empty<string>();

    /// <summary>回调 URL 配置中的 token（用于 SHA1 签名）。</summary>
    public string? CallbackToken { get; init; }

    /// <summary>回调 URL 配置中的 EncodingAESKey（base64 + "=" 后 43 位）。</summary>
    public string? CallbackEncodingAesKey { get; init; }
}
