using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Security;
using Atlas.Connectors.WeCom.Internal;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企业微信回调验签 + AES-CBC 解密。官方 WXBizMsgCrypt 算法：
/// 1. 校验 SHA1(token, timestamp, nonce, encryptedBody) == msg_signature；
/// 2. EncodingAESKey + "=" 后 base64 decode → 32 字节 AES key（前 16 字节为 IV）；
/// 3. 解密 + PKCS#7 去填充 + 跳过前 16 字节随机串 + 读 4 字节网络序消息长度 + N 字节 UTF-8 XML + 尾部 corpId；
/// 4. 强制校验尾部 corpId 与 RuntimeOptions 一致（原实现缺失，已补齐）；
/// 5. XML 转 JSON（通过 <see cref="WeComXmlJsonConverter"/>，已替换 rawXml 占位）。
///
/// Token / EncodingAESKey / CorpId 通过 Headers["x-wecom-token"] / ["x-wecom-encoding-aes-key"] / ["x-wecom-corpid"] 由
/// <c>ConnectorCallbacksController</c> 在解析到 ProviderRuntimeOptions 后写入，避免 Core 反向依赖 Infrastructure。
/// </summary>
public sealed class WeComCallbackVerifier : IConnectorEventVerifier
{
    public string ProviderType => WeComConnectorMarker.ProviderType;

    public ConnectorWebhookEnvelope Verify(IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> headers, byte[] body)
    {
        var msgSignature = GetCaseInsensitive(query, "msg_signature");
        var timestamp = GetCaseInsensitive(query, "timestamp");
        var nonce = GetCaseInsensitive(query, "nonce");
        if (string.IsNullOrEmpty(msgSignature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(nonce))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookSignatureInvalid, "WeCom callback missing msg_signature / timestamp / nonce.", ProviderType);
        }

        var token = GetCaseInsensitive(headers, "x-wecom-token");
        var aesKey = GetCaseInsensitive(headers, "x-wecom-encoding-aes-key");
        var expectedCorpId = GetCaseInsensitive(headers, "x-wecom-corpid");
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(aesKey) || string.IsNullOrEmpty(expectedCorpId))
        {
            throw new ConnectorException(
                ConnectorErrorCodes.WebhookDecryptFailed,
                "WeCom callback verifier requires x-wecom-token / x-wecom-encoding-aes-key / x-wecom-corpid headers (set by ConnectorCallbacksController).",
                ProviderType);
        }

        var bodyText = Encoding.UTF8.GetString(body);
        var encryptedXml = ExtractEncryptedXml(bodyText);
        var computed = HmacValidator.ComputeWeComStyleSignature(token, timestamp, nonce, encryptedXml);
        if (!HmacValidator.FixedTimeEquals(computed, msgSignature))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookSignatureInvalid, "WeCom callback signature mismatch.", ProviderType);
        }

        var (plainXml, decryptedCorpId) = DecryptAesCbc(encryptedXml, aesKey);
        if (!string.Equals(decryptedCorpId, expectedCorpId, StringComparison.Ordinal))
        {
            // WXBizMsgCrypt 规范强制校验：解密后尾部 corpId 必须等于当前配置的 corp，否则为伪造回调。
            throw new ConnectorException(
                ConnectorErrorCodes.WebhookDecryptFailed,
                $"WeCom callback decrypted corpId '{decryptedCorpId}' does not match configured corp '{expectedCorpId}'.",
                ProviderType);
        }

        var topic = ExtractTopicFromXml(plainXml);
        return new ConnectorWebhookEnvelope
        {
            ProviderType = ProviderType,
            Topic = topic,
            PayloadJson = WeComXmlJsonConverter.ToJson(plainXml),
            IdempotencyKey = ExtractIdempotencyKey(plainXml, timestamp, nonce),
        };
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

    private static string ExtractEncryptedXml(string bodyText)
    {
        var doc = new XmlDocument();
        doc.LoadXml(bodyText);
        var node = doc.SelectSingleNode("/xml/Encrypt");
        return node?.InnerText ?? string.Empty;
    }

    private static (string Plain, string CorpId) DecryptAesCbc(string encryptedBase64, string encodingAesKey)
    {
        var keyBytes = Convert.FromBase64String(encodingAesKey + "=");
        if (keyBytes.Length != 32)
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "WeCom EncodingAESKey decode length must be 32.", WeComConnectorMarker.ProviderType);
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
        var unpaddedLength = decrypted.Length - pad;

        var content = decrypted.AsSpan(16, unpaddedLength - 16);
        var msgLen = (content[0] << 24) | (content[1] << 16) | (content[2] << 8) | content[3];
        if (msgLen < 0 || 4 + msgLen > content.Length)
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "WeCom callback length prefix is invalid.", WeComConnectorMarker.ProviderType);
        }
        var xmlBytes = content.Slice(4, msgLen);
        var corpIdBytes = content.Slice(4 + msgLen);
        var xml = Encoding.UTF8.GetString(xmlBytes);
        var corpId = Encoding.UTF8.GetString(corpIdBytes);
        return (xml, corpId);
    }

    private static string ExtractTopicFromXml(string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc.SelectSingleNode("/xml/Event")?.InnerText
                ?? doc.SelectSingleNode("/xml/MsgType")?.InnerText
                ?? "unknown";
        }
        catch (XmlException)
        {
            return "unknown";
        }
    }

    private static string ExtractIdempotencyKey(string xml, string timestamp, string nonce)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var spNo = doc.SelectSingleNode("/xml/ApprovalInfo/SpNo")?.InnerText
                ?? doc.SelectSingleNode("/xml/ApprovalInfo/sp_no")?.InnerText;
            var status = doc.SelectSingleNode("/xml/ApprovalInfo/SpStatus")?.InnerText;
            if (!string.IsNullOrEmpty(spNo))
            {
                return string.Concat(spNo, ":", status, ":", timestamp);
            }
        }
        catch (XmlException)
        {
        }
        return string.Concat(timestamp, ":", nonce);
    }
}
