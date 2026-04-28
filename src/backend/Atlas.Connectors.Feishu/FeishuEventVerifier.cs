using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Security;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 飞书事件订阅 v2 验签 + 解密：
/// - 签名头：X-Lark-Signature = SHA256(EncryptKey + timestamp + nonce + body)；
/// - 加密 body 字段：encrypt（base64）；解密用 EncryptKey（SHA256 后取 32 字节作为 AES-256-CBC key）。
/// </summary>
public sealed class FeishuEventVerifier : IConnectorEventVerifier
{
    public string ProviderType => FeishuConnectorMarker.ProviderType;

    public ConnectorWebhookEnvelope Verify(IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> headers, byte[] body)
    {
        var signature = GetCaseInsensitive(headers, "x-lark-signature");
        var timestamp = GetCaseInsensitive(headers, "x-lark-request-timestamp");
        var nonce = GetCaseInsensitive(headers, "x-lark-request-nonce");
        var encryptKey = GetCaseInsensitive(headers, "x-feishu-encrypt-key");
        var verificationToken = GetCaseInsensitive(headers, "x-feishu-verification-token");
        if (string.IsNullOrEmpty(encryptKey) || string.IsNullOrEmpty(verificationToken))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "Feishu callback verifier requires x-feishu-encrypt-key and x-feishu-verification-token headers (set by ConnectorCallbacksController).", ProviderType);
        }

        var bodyText = Encoding.UTF8.GetString(body);

        if (!string.IsNullOrEmpty(signature))
        {
            var expected = ComputeSha256Hex(encryptKey + timestamp + nonce + bodyText);
            if (!HmacValidator.FixedTimeEquals(expected, signature))
            {
                throw new ConnectorException(ConnectorErrorCodes.WebhookSignatureInvalid, "Feishu signature mismatch.", ProviderType);
            }
        }

        var encryptValue = ExtractEncryptedBody(bodyText);
        var plain = encryptValue is null ? bodyText : DecryptAesCbc(encryptValue, encryptKey);

        // 校验 token
        try
        {
            using var doc = JsonDocument.Parse(plain);
            if (doc.RootElement.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String)
            {
                if (!HmacValidator.FixedTimeEquals(verificationToken, token.GetString() ?? string.Empty))
                {
                    throw new ConnectorException(ConnectorErrorCodes.WebhookSignatureInvalid, "Feishu verification token mismatch.", ProviderType);
                }
            }

            var topic = doc.RootElement.TryGetProperty("header", out var header) && header.TryGetProperty("event_type", out var et) && et.ValueKind == JsonValueKind.String
                ? et.GetString() ?? "unknown"
                : (doc.RootElement.TryGetProperty("event", out var eventNode) && eventNode.TryGetProperty("type", out var et2) ? et2.GetString() ?? "unknown" : "unknown");
            var eventId = doc.RootElement.TryGetProperty("header", out var headerForId) && headerForId.TryGetProperty("event_id", out var eid) ? eid.GetString() : null;
            return new ConnectorWebhookEnvelope
            {
                ProviderType = ProviderType,
                Topic = topic,
                PayloadJson = plain,
                IdempotencyKey = string.IsNullOrEmpty(eventId) ? string.Concat(timestamp, ":", nonce) : eventId,
            };
        }
        catch (JsonException ex)
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "Feishu payload not valid JSON after decryption.", ProviderType, innerException: ex);
        }
    }

    private static string? ExtractEncryptedBody(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("encrypt", out var enc) && enc.ValueKind == JsonValueKind.String)
            {
                return enc.GetString();
            }
        }
        catch (JsonException) { }
        return null;
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string DecryptAesCbc(string encryptedBase64, string encryptKey)
    {
        // 飞书 EncryptKey → SHA256 → 32 字节 AES-256 key；IV 取密文前 16 字节
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(encryptKey));
        var cipher = Convert.FromBase64String(encryptedBase64);
        if (cipher.Length < 16)
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "Feishu encrypted body too short.", FeishuConnectorMarker.ProviderType);
        }
        var iv = cipher.Take(16).ToArray();
        var actualCipher = cipher.Skip(16).ToArray();

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(actualCipher, 0, actualCipher.Length);
        return Encoding.UTF8.GetString(plain);
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
