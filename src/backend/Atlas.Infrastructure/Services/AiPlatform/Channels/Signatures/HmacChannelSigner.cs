using System;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Signatures;

/// <summary>
/// 渠道入站签名工具：Web SDK 浏览器侧用 HMAC-SHA256 签 (timestamp + nonce + body) 后随请求头下发，
/// 服务端在 connector.HandleInboundAsync 验签 + 校验 timestamp 漂移 + 防重放（nonce）。
/// 设计：
/// - HMAC 输入串：<c>{timestamp}\n{nonce}\n{body}</c>，避免 query 顺序影响；
/// - timestamp 单位 unix-seconds，默认允许 ±300 秒漂移；
/// - nonce 由调用方维护去重表，本工具只验证签名一致性。
/// </summary>
public static class HmacChannelSigner
{
    /// <summary>默认时间戳偏移容忍度（秒）。</summary>
    public const int DefaultClockSkewSeconds = 300;

    public static string Compute(string secret, long unixTimestampSeconds, string nonce, string body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentException.ThrowIfNullOrEmpty(nonce);
        var raw = $"{unixTimestampSeconds}\n{nonce}\n{body ?? string.Empty}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return ToHex(bytes);
    }

    public static bool Verify(
        string secret,
        long unixTimestampSeconds,
        string nonce,
        string body,
        string presentedSignature,
        long? nowUnixSeconds = null,
        int clockSkewSeconds = DefaultClockSkewSeconds)
    {
        if (string.IsNullOrEmpty(presentedSignature))
        {
            return false;
        }
        var now = nowUnixSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - unixTimestampSeconds) > clockSkewSeconds)
        {
            return false;
        }
        var expected = Compute(secret, unixTimestampSeconds, nonce, body);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(presentedSignature));
    }

    /// <summary>生成长度为 32 的 base64url 安全 token，可作为 HMAC 主密钥或 embed token。</summary>
    public static string GenerateSecret(int byteLength = 32)
    {
        if (byteLength < 16)
        {
            byteLength = 16;
        }
        var buffer = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(buffer);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        var s = Convert.ToBase64String(data);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
