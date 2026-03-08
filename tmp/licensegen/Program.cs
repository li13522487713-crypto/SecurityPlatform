using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var tenantId = "00000000-0000-0000-0000-000000000001";
var issuedAt = DateTimeOffset.UtcNow;

using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();

var payload = new LicensePayload
{
    LicenseId = Guid.NewGuid(),
    Revision = 1,
    CustomerId = "atlas-demo-customer",
    TenantName = "Atlas Demo Tenant",
    TenantId = tenantId,
    IssuedAt = issuedAt,
    ExpiresAt = issuedAt.AddDays(365),
    IsPermanent = false,
    Edition = "Enterprise",
    MachineFingerprint = null,
    Features = new Dictionary<string, bool>
    {
        ["lowCode"] = true,
        ["workflow"] = true,
        ["approval"] = true,
        ["alert"] = true,
        ["offlineDeploy"] = true,
        ["multiTenant"] = true,
        ["audit"] = true
    },
    Limits = new Dictionary<string, int>
    {
        ["maxApps"] = 200,
        ["maxUsers"] = 2000,
        ["maxTenants"] = 20,
        ["maxDataSources"] = 100,
        ["auditRetentionDays"] = 365
    }
};

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};

var payloadJson = JsonSerializer.Serialize(payload, options);
var signatureBytes = ecdsa.SignData(Encoding.UTF8.GetBytes(payloadJson), HashAlgorithmName.SHA256);
var signature = Convert.ToBase64String(signatureBytes);

var envelope = new LicenseEnvelope
{
    Header = new LicenseHeader
    {
        Version = "1.0",
        Algorithm = "ECDSA-SHA256",
        KeyId = "atlas-local-dev-20260308"
    },
    Payload = payload,
    Signature = signature
};

var envelopeJson = JsonSerializer.Serialize(envelope, options);
var licenseContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(envelopeJson));

var outDir = Path.Combine(Directory.GetCurrentDirectory(), "out");
Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "public_key.pem"), publicPem);
File.WriteAllText(Path.Combine(outDir, "license.txt"), licenseContent);

Console.WriteLine("PUBLIC_KEY_PEM_BEGIN");
Console.WriteLine(publicPem.Trim());
Console.WriteLine("PUBLIC_KEY_PEM_END");
Console.WriteLine("LICENSE_CONTENT_BEGIN");
Console.WriteLine(licenseContent);
Console.WriteLine("LICENSE_CONTENT_END");

public sealed class LicenseHeader
{
    public string Version { get; set; } = "1.0";
    public string Algorithm { get; set; } = "ECDSA-SHA256";
    public string KeyId { get; set; } = "atlas-local-dev";
}

public sealed class LicensePayload
{
    public Guid LicenseId { get; set; }
    public int Revision { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsPermanent { get; set; }
    public string Edition { get; set; } = "Trial";
    public string? MachineFingerprint { get; set; }
    public Dictionary<string, bool> Features { get; set; } = new();
    public Dictionary<string, int> Limits { get; set; } = new();
}

public sealed class LicenseEnvelope
{
    public LicenseHeader Header { get; set; } = new();
    public LicensePayload Payload { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
}
