namespace Atlas.LicenseIssuer.Models;

public sealed class LicensePayload
{
    public Guid LicenseId { get; set; }
    public int Revision { get; set; } = 1;
    public string CustomerId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    /// <summary>平台租户 GUID（由颁发工具从 CustomerRecord.TenantId 写入，平台激活时用于自动关联租户）</summary>
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
