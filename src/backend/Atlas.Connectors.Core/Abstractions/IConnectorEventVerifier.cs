namespace Atlas.Connectors.Core.Abstractions;

/// <summary>
/// 入站 webhook 的验签 / 解密能力。各 provider 实现自己的 XML+AES（企微）或 Encrypt+VerificationToken（飞书）逻辑。
/// </summary>
public interface IConnectorEventVerifier
{
    string ProviderType { get; }

    /// <summary>
    /// 校验签名，验签失败抛 ConnectorException(WebhookSignatureInvalid)。
    /// 解密成功后返回明文 JSON / XML 字符串，由上层再交给具体 handler 解析。
    /// </summary>
    ConnectorWebhookEnvelope Verify(IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> headers, byte[] body);
}

/// <summary>
/// Verify 完成后的 envelope，包含明文 payload 与 provider 的事件元数据。
/// </summary>
public sealed record ConnectorWebhookEnvelope
{
    public required string ProviderType { get; init; }

    public required string Topic { get; init; }

    public required string PayloadJson { get; init; }

    public string? IdempotencyKey { get; init; }

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyDictionary<string, string>? Extra { get; init; }
}
