using System.Security.Cryptography;
using System.Text;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 单个 WeCom provider 实例的运行时配置（已解密）。
/// 由 Application 层 IConnectorRuntimeOptionsAccessor 解析后通过 ConnectorContext.RuntimeOptions 传给 Connectors.WeCom 底层库。
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

    /// <summary>
    /// 凭据指纹：用于 access_token 缓存键拼接，运维改 secret 后旧 key 自然失效。
    /// 取 SHA-256(CorpId + ":" + CorpSecret + ":" + AgentId) 前 8 字符 hex。
    /// </summary>
    internal string GetCredentialFingerprint()
    {
        var raw = string.Concat(CorpId, ":", CorpSecret, ":", AgentId);
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }
}
