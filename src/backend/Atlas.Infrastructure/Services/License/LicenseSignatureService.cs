using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.License.Abstractions;
using Atlas.Application.License.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.License;

/// <summary>
/// 证书签名验证服务：用内嵌的 ECDSA P-256 公钥验证证书签名。
/// 私钥仅存在于颁发工具（Atlas.LicenseIssuer），平台只有公钥。
/// </summary>
public sealed class LicenseSignatureService : ILicenseSignatureService
{
    // 内嵌公钥（PEM 格式）。颁发工具生成密钥对后，将公钥嵌入此处再发布。
    // ⚠️ 此为开发期占位符。发布前必须替换为颁发工具导出的实际公钥，否则生产启动将失败。
    private const string EmbeddedPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEPLAeG/ADI5HmfBGNNY3Y7v0bS5zx
        Xo7Fp3tGKY6sU5Y+RJnGNMvY0Gh+wXMpq3DhKnF+sDqFZmKR7v0HdLPZpA==
        -----END PUBLIC KEY-----
        """;

    // Base64 主体（去除 PEM 头尾与空白）对应上方占位公钥。
    // 用于启动时与 EmbeddedPublicKeyPem 的实际内容比对，防止占位符流入生产。
    private const string PlaceholderKeyBody =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEPLAeG/ADI5HmfBGNNY3Y7v0bS5zx" +
        "Xo7Fp3tGKY6sU5Y+RJnGNMvY0Gh+wXMpq3DhKnF+sDqFZmKR7v0HdLPZpA==";

    private readonly ILogger<LicenseSignatureService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public LicenseSignatureService(ILogger<LicenseSignatureService> logger, IHostEnvironment environment)
    {
        _logger = logger;
        GuardAgainstPlaceholderKey(environment.IsProduction());
    }

    /// <summary>
    /// 检测 EmbeddedPublicKeyPem 是否仍为占位符。
    /// 生产环境抛出异常阻止启动；开发环境记录警告允许继续运行。
    /// </summary>
    private void GuardAgainstPlaceholderKey(bool isProduction)
    {
        var pemBody = string.Concat(
            EmbeddedPublicKeyPem
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !l.StartsWith("-----")));

        if (pemBody != PlaceholderKeyBody)
            return;

        const string guidance =
            "EmbeddedPublicKeyPem 仍为开发期占位符公钥。" +
            "请使用 Atlas.LicenseIssuer 生成密钥对，将导出的公钥替换 LicenseSignatureService 中的常量后重新编译发布。";

        if (isProduction)
            throw new InvalidOperationException($"[License] 生产环境安全检查失败：{guidance}");

        _logger.LogWarning("[License] 开发环境警告：{Guidance}", guidance);
    }

    public LicenseEnvelope? Parse(string rawContent)
    {
        try
        {
            var normalized = rawContent.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            // 兼容两种证书文件格式：
            // 1) 直接存放 JSON envelope；2) 以 Base64 封装 JSON envelope。
            if (TryDeserializeEnvelope(normalized, out var envelope))
            {
                return envelope;
            }

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            return TryDeserializeEnvelope(decoded, out envelope) ? envelope : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "证书解析失败");
            return null;
        }
    }

    private static bool TryDeserializeEnvelope(string json, out LicenseEnvelope? envelope)
    {
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(json, _jsonOptions);
            return envelope is not null;
        }
        catch
        {
            envelope = null;
            return false;
        }
    }

    public bool Verify(LicenseEnvelope envelope)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(EmbeddedPublicKeyPem);

            var payloadJson = JsonSerializer.Serialize(envelope.Payload, _jsonOptions);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var signatureBytes = Convert.FromBase64String(envelope.Signature);

            return ecdsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "证书签名验证失败");
            return false;
        }
    }
}
