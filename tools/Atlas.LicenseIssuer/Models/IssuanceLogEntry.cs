using SqlSugar;

namespace Atlas.LicenseIssuer.Models;

[SugarTable("issuance_log")]
public sealed class IssuanceLogEntry
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string LicenseId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string Edition { get; set; } = string.Empty;

    /// <summary>NEW / RENEW / UPGRADE / REVOKE</summary>
    public string Action { get; set; } = "NEW";
    public string? Operator { get; set; }
    public string IssuedAt { get; set; } = DateTimeOffset.UtcNow.ToString("o");
    public string? ExpiresAt { get; set; }
    public bool IsPermanent { get; set; }
    public string? Remark { get; set; }
}
