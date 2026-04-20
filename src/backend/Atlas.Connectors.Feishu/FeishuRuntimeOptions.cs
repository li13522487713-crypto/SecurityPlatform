using System.Security.Cryptography;
using System.Text;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 单个飞书 provider 实例的运行时配置（已解密）。
/// 由 Application 层 IConnectorRuntimeOptionsAccessor 解析后通过 ConnectorContext.RuntimeOptions 传给 Connectors.Feishu 底层库。
/// </summary>
public sealed record FeishuRuntimeOptions
{
    public required string AppId { get; init; }

    public required string AppSecret { get; init; }

    /// <summary>飞书租户 key，用于跨租户识别。</summary>
    public string? TenantKey { get; init; }

    public required string CallbackBaseUrl { get; init; }

    public IReadOnlyList<string> TrustedDomains { get; init; } = Array.Empty<string>();

    /// <summary>事件订阅 Verification Token（明文）。</summary>
    public string? EventVerificationToken { get; init; }

    /// <summary>事件订阅 Encrypt Key（base64 后 32 字节，AES-256）。</summary>
    public string? EventEncryptKey { get; init; }

    /// <summary>
    /// 凭据指纹：用于 token 缓存键拼接，运维改 secret 后旧 key 自然失效。
    /// 取 SHA-256(AppId + ":" + AppSecret) 前 8 字符 hex。
    /// </summary>
    internal string GetCredentialFingerprint()
    {
        var raw = string.Concat(AppId, ":", AppSecret);
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }
}
