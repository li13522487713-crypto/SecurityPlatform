using System.Security.Cryptography;
using System.Text;

namespace Atlas.Connectors.Core.Security;

/// <summary>
/// 通用 HMAC-SHA1 / HMAC-SHA256 计算与定时安全比较工具，便于各 provider 重用。
/// </summary>
public static class HmacValidator
{
    public static string ComputeHmacSha256Hex(string secret, ReadOnlySpan<char> data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data.ToArray());
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeHmacSha256Hex(string secret, byte[] data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(keyBytes, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeSha1Hex(ReadOnlySpan<char> data)
    {
        var bytes = Encoding.UTF8.GetBytes(data.ToArray());
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 企业微信 / 飞书一致的"按字典序拼接 token + timestamp + nonce + body"再做 SHA1 的常用流程。
    /// </summary>
    public static string ComputeWeComStyleSignature(string token, string timestamp, string nonce, string encryptedBody)
    {
        var arr = new[] { token, timestamp, nonce, encryptedBody };
        Array.Sort(arr, StringComparer.Ordinal);
        var joined = string.Concat(arr);
        return ComputeSha1Hex(joined);
    }

    /// <summary>
    /// 定时安全比较，避免因比较时长泄漏密钥。
    /// </summary>
    public static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
