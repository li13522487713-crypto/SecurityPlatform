using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 治理 M-G08-C1（S15）：租户网络策略（IP 白名单 + 拒绝模式）。
/// Mode：audit（仅记录命中）/ enforce（拒绝命中 deny / 不命中 allow）。
/// </summary>
[SugarTable("TenantNetworkPolicy")]
public sealed class TenantNetworkPolicy : TenantEntity
{
    public const string ModeAudit = "audit";
    public const string ModeEnforce = "enforce";

    public TenantNetworkPolicy()
        : base(TenantId.Empty)
    {
        Mode = ModeAudit;
        AllowlistJson = "[]";
        DenylistJson = "[]";
        UpdatedAt = DateTime.UtcNow;
    }

    public TenantNetworkPolicy(
        TenantId tenantId,
        string mode,
        string allowlistJson,
        string denylistJson,
        long updatedBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        Mode = string.Equals(mode, ModeEnforce, StringComparison.OrdinalIgnoreCase) ? ModeEnforce : ModeAudit;
        AllowlistJson = string.IsNullOrWhiteSpace(allowlistJson) ? "[]" : allowlistJson;
        DenylistJson = string.IsNullOrWhiteSpace(denylistJson) ? "[]" : denylistJson;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 16, IsNullable = false)]
    public string Mode { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string AllowlistJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string DenylistJson { get; private set; }

    public long UpdatedBy { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string mode, string allowlistJson, string denylistJson, long updatedBy)
    {
        Mode = string.Equals(mode, ModeEnforce, StringComparison.OrdinalIgnoreCase) ? ModeEnforce : ModeAudit;
        AllowlistJson = string.IsNullOrWhiteSpace(allowlistJson) ? "[]" : allowlistJson;
        DenylistJson = string.IsNullOrWhiteSpace(denylistJson) ? "[]" : denylistJson;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
