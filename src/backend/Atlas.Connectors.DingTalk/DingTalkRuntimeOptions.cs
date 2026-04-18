namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 单个 DingTalk provider 实例的运行时配置（已解密）。
/// </summary>
public sealed record DingTalkRuntimeOptions
{
    public required string AppKey { get; init; }

    public required string AppSecret { get; init; }

    /// <summary>企业 CorpId（可选，回调过滤与日志标识用）。</summary>
    public string? CorpId { get; init; }

    /// <summary>工作通知 AgentId；内建应用需要，机器人消息无需。</summary>
    public string? AgentId { get; init; }

    public required string CallbackBaseUrl { get; init; }

    public IReadOnlyList<string> TrustedDomains { get; init; } = Array.Empty<string>();

    /// <summary>事件订阅 AES Key（base64 后 32 字节），用于回调加解密。</summary>
    public string? CallbackAesKey { get; init; }

    /// <summary>事件订阅 Token，用于签名校验。</summary>
    public string? CallbackToken { get; init; }
}
