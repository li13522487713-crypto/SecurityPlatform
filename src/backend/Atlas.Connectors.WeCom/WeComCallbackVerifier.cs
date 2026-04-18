using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Security;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企业微信回调验签 + AES-CBC 解密。流程：
/// 1. 校验 SHA1(token, timestamp, nonce, encryptedBody) == msg_signature；
/// 2. 用 EncodingAESKey 解密 encryptedBody；
/// 3. 解密结果前 16 字节为随机串、接着 4 字节网络序消息长度、再接 N 字节 XML、再接 corpId。
/// 4. 把 XML 转 JSON 透传给 handler 处理。
///
/// 实现仅依赖 WeComApiClient.ResolveRuntimeOptionsAsync（取出 token + EncodingAESKey + corpId）。
/// 注意：这里我们让调用方在 Verify 之前先把 RuntimeOptions 解析好并放进 Headers["wecom_runtime"]，
/// 简化对 ConnectorContext 的反向依赖。
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
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(aesKey))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookDecryptFailed, "WeCom callback verifier requires x-wecom-token and x-wecom-encoding-aes-key headers (set by ConnectorCallbacksController).", ProviderType);
        }

        var bodyText = Encoding.UTF8.GetString(body);
        var encryptedXml = ExtractEncryptedXml(bodyText);
        var computed = HmacValidator.ComputeWeComStyleSignature(token, timestamp, nonce, encryptedXml);
        if (!HmacValidator.FixedTimeEquals(computed, msgSignature))
        {
            throw new ConnectorException(ConnectorErrorCodes.WebhookSignatureInvalid, "WeCom callback signature mismatch.", ProviderType);
        }

        var (plainXml, _) = DecryptAesCbc(encryptedXml, aesKey);
        var topic = ExtractTopicFromXml(plainXml);
        return new ConnectorWebhookEnvelope
        {
            ProviderType = ProviderType,
            Topic = topic,
            PayloadJson = XmlToJson(plainXml),
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
        // <xml><Encrypt><![CDATA[...]]></Encrypt></xml>
        var doc = new XmlDocument();
        doc.LoadXml(bodyText);
        var node = doc.SelectSingleNode("/xml/Encrypt");
        return node?.InnerText ?? string.Empty;
    }

    private static (string Plain, string CorpId) DecryptAesCbc(string encryptedBase64, string encodingAesKey)
    {
        // EncodingAESKey 是 43 位 base64 字符，补 "=" 后 base64 decode 得到 32 字节 AES key（=IV 前 16 字节）。
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
        // 移除 PKCS#7 风格 padding（企微补码自定）
        var pad = decrypted[^1];
        if (pad < 1 || pad > 32) pad = 0;
        var unpaddedLength = decrypted.Length - pad;

        // 前 16 字节随机串
        var content = decrypted.AsSpan(16, unpaddedLength - 16);
        // 接 4 字节 length（网络序）
        var msgLen = (content[0] << 24) | (content[1] << 16) | (content[2] << 8) | content[3];
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

    private static string XmlToJson(string xml)
    {
        // 简化版：直接返回 JSON 字符串包装 xml 原文，由上层 handler 用具体 XML 解析。
        // 真实生产可换 LINQ-to-XML → JsonObject 转换。
        return System.Text.Json.JsonSerializer.Serialize(new { rawXml = xml });
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
        catch (XmlException) { }
        return string.Concat(timestamp, ":", nonce);
    }
}
