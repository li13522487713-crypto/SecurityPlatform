using System.Security.Cryptography;
using System.Text;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 单个 DingTalk provider 实例的运行时配置（已解密）。
/// 由 Application 层 IConnectorRuntimeOptionsAccessor 解析后通过 ConnectorContext.RuntimeOptions 传给 Connectors.DingTalk 底层库。
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

    /// <summary>
    /// 凭据指纹：用于 access_token 缓存键拼接，运维改 secret 后旧 key 自然失效。
    /// 取 SHA-256(AppKey + ":" + AppSecret + ":" + AgentId) 前 8 字符 hex。
    /// </summary>
    internal string GetCredentialFingerprint()
    {
        var raw = string.Concat(AppKey, ":", AppSecret, ":", AgentId ?? string.Empty);
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }
}
