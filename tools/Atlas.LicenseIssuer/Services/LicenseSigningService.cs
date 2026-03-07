using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.LicenseIssuer.Models;

namespace Atlas.LicenseIssuer.Services;

public sealed class LicenseSigningService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly KeyManagementService _keyManagement;

    public LicenseSigningService(KeyManagementService keyManagement)
    {
        _keyManagement = keyManagement;
    }

    /// <summary>对 Payload 签名，返回完整的 LicenseEnvelope（Base64 编码）</summary>
    public string SignAndExport(LicensePayload payload)
    {
        var ecdsa = _keyManagement.GetPrivateKey();

        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var signatureBytes = ecdsa.SignData(payloadBytes, HashAlgorithmName.SHA256);
        var signature = Convert.ToBase64String(signatureBytes);

        var envelope = new LicenseEnvelope
        {
            Header = new LicenseHeader(),
            Payload = payload,
            Signature = signature
        };

        var envelopeJson = JsonSerializer.Serialize(envelope, _jsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(envelopeJson));
    }

    /// <summary>用公钥自检已签名的证书内容</summary>
    public bool VerifyExported(string exportedContent)
    {
        try
        {
            using var verifyEcdsa = ECDsa.Create();
            verifyEcdsa.ImportFromPem(_keyManagement.ExportPublicKeyPem());

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(exportedContent));
            var envelope = JsonSerializer.Deserialize<LicenseEnvelope>(decoded, _jsonOptions);
            if (envelope is null) return false;

            var payloadJson = JsonSerializer.Serialize(envelope.Payload, _jsonOptions);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var signatureBytes = Convert.FromBase64String(envelope.Signature);
            return verifyEcdsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }
}
