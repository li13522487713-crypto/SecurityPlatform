namespace Atlas.LicenseIssuer.Models;

public sealed class LicenseHeader
{
    public string Version { get; set; } = "1.0";
    public string Algorithm { get; set; } = "ECDSA-SHA256";
    public string Kid { get; set; } = "atlas-2026-01";
}
