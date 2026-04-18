using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 钉钉事件订阅 v1 验签 + 解密：
/// - signature = Base64( HMAC-SHA256(token, AES_encryptedBody + timestamp + nonce) )（与企微 WXBizMsgCrypt 思路一致，但用 HMAC-SHA256）；
/// - encrypt 字段：base64(IV(16) + paddedBody(includes 4-byte big-endian length + payload + corpId))；AES-256-CBC；
/// - 解密后剥离前 16 字节随机串、4 字节长度、payload、尾部 corpId（强制校验）。
/// 调用方需通过 headers 注入：x-dingtalk-token / x-dingtalk-aes-key / x-dingtalk-corpid（由 ConnectorCallbacksController 注入）。
/// </summary>
public sealed class DingTalkCallbackVerifier : IConnectorEventVerifier
{
    public string ProviderType => DingTalkConnectorMarker.ProviderType;

    public ConnectorWebhookEnvelope Verify(IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> headers, byte[] body)
    {
        var signature = GetCaseInsensitive(query, "signature");
        var timestamp = GetCaseInsensitive(query, "timestamp");
        var nonce = GetCaseInsensitive(query, "nonce");
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(nonce))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookSignatureInvalid, "DingTalk callback missing signature/timestamp/nonce.", ProviderType);
        }

        var token = GetCaseInsensitive(headers, "x-dingtalk-token");
        var aesKey = GetCaseInsensitive(headers, "x-dingtalk-aes-key");
        var expectedCorpId = GetCaseInsensitive(headers, "x-dingtalk-corpid");
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(aesKey) || string.IsNullOrEmpty(expectedCorpId))
        {
            throw new ConnectorException(
                ConnectorErrorCodes.WebhookDecryptFailed,
                "DingTalk callback verifier requires x-dingtalk-token / x-dingtalk-aes-key / x-dingtalk-corpid headers.",
                ProviderType);
        }

        var bodyText = Encoding.UTF8.GetString(body);
        var encryptValue = ExtractEncryptedBody(bodyText);
        if (string.IsNullOrEmpty(encryptValue))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "DingTalk callback body has no encrypt field.", ProviderType);
        }

        var expected = ComputeSignature(token, encryptValue!, timestamp, nonce);
        if (!FixedTimeEquals(expected, signature))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookSignatureInvalid, "DingTalk callback signature mismatch.", ProviderType);
        }

        var (plain, decryptedCorpId) = DecryptAesCbc(encryptValue!, aesKey);
        if (!string.Equals(decryptedCorpId, expectedCorpId, StringComparison.Ordinal))
        {
            throw new ConnectorException(
                ConnectorErrorCodes.WebhookDecryptFailed,
                $"DingTalk callback decrypted corpId '{decryptedCorpId}' does not match configured corp '{expectedCorpId}'.",
                ProviderType);
        }

        var topic = ExtractTopic(plain);
        var idempotencyKey = ExtractIdempotencyKey(plain, timestamp, nonce);
        return new ConnectorWebhookEnvelope
        {
            ProviderType = ProviderType,
            Topic = topic,
            PayloadJson = plain,
            IdempotencyKey = idempotencyKey,
        };
    }

    private static string ComputeSignature(string token, string encryptedBody, string timestamp, string nonce)
    {
        // 钉钉签名规则：sort([token, timestamp, nonce, encrypted])，concat 后 SHA1 → hex
        var arr = new[] { token, timestamp, nonce, encryptedBody };
        Array.Sort(arr, StringComparer.Ordinal);
        var joined = string.Concat(arr);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? ExtractEncryptedBody(string bodyText)
    {
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.TryGetProperty("encrypt", out var enc) && enc.ValueKind == JsonValueKind.String)
            {
                return enc.GetString();
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    private static (string Plain, string CorpId) DecryptAesCbc(string encryptedBase64, string aesKey)
    {
        var keyBytes = Convert.FromBase64String(aesKey + "=");
        if (keyBytes.Length != 32)
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "DingTalk AES key decode length must be 32.", DingTalkConnectorMarker.ProviderType);
        }
        var iv = keyBytes.Take(16).ToArray();

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.IV = iv;

        var cipher = Convert.FromBase64String(encryptedBase64);
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        var pad = decrypted[^1];
        if (pad < 1 || pad > 32)
        {
            pad = 0;
        }
        var unpadded = decrypted.AsSpan(0, decrypted.Length - pad);

        // 与企微相同：前 16 字节随机串 + 4 字节大端长度 + payload + corpId
        var content = unpadded[16..];
        var msgLen = (content[0] << 24) | (content[1] << 16) | (content[2] << 8) | content[3];
        if (msgLen < 0 || 4 + msgLen > content.Length)
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "DingTalk callback length prefix is invalid.", DingTalkConnectorMarker.ProviderType);
        }
        var payload = Encoding.UTF8.GetString(content.Slice(4, msgLen));
        var corpId = Encoding.UTF8.GetString(content.Slice(4 + msgLen));
        return (payload, corpId);
    }

    private static string ExtractTopic(string plain)
    {
        try
        {
            using var doc = JsonDocument.Parse(plain);
            if (doc.RootElement.TryGetProperty("EventType", out var et) && et.ValueKind == JsonValueKind.String)
            {
                return et.GetString() ?? "unknown";
            }
            if (doc.RootElement.TryGetProperty("eventType", out var et2) && et2.ValueKind == JsonValueKind.String)
            {
                return et2.GetString() ?? "unknown";
            }
        }
        catch (JsonException)
        {
        }
        return "unknown";
    }

    private static string ExtractIdempotencyKey(string plain, string timestamp, string nonce)
    {
        try
        {
            using var doc = JsonDocument.Parse(plain);
            // 钉钉事件常见唯一键：processInstanceId + EventType
            var instanceId = doc.RootElement.TryGetProperty("processInstanceId", out var pid) ? pid.GetString() : null;
            var eventType = doc.RootElement.TryGetProperty("EventType", out var et) ? et.GetString() : null;
            if (!string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(eventType))
            {
                return string.Concat(eventType, ":", instanceId, ":", timestamp);
            }
        }
        catch (JsonException)
        {
        }
        return string.Concat(timestamp, ":", nonce);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left is null || right is null || left.Length != right.Length)
        {
            return false;
        }
        var diff = 0;
        for (var i = 0; i < left.Length; i++)
        {
            diff |= left[i] ^ right[i];
        }
        return diff == 0;
    }

    private static string GetCaseInsensitive(IReadOnlyDictionary<string, string> dict, string key)
    {
        foreach (var kv in dict)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }
        return string.Empty;
    }
}
