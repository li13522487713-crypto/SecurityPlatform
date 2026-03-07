using SqlSugar;

namespace Atlas.LicenseIssuer.Models;

[SugarTable("customers")]
public sealed class CustomerRecord
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Remark { get; set; }
    /// <summary>对应平台侧的租户 GUID（可选；颁发证书时写入 Payload.TenantId）</summary>
    public string? TenantId { get; set; }
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("o");
}
