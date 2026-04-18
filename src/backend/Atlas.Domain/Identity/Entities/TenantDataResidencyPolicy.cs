using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 治理 M-G08-C2（S15）：租户数据驻留策略（region 限制）。
/// 存储端点解析器（IObjectStorageEndpointResolver）会读取本表，跨区写入时直接拒绝并审计。
/// </summary>
[SugarTable("TenantDataResidencyPolicy")]
public sealed class TenantDataResidencyPolicy : TenantEntity
{
    public TenantDataResidencyPolicy()
        : base(TenantId.Empty)
    {
        AllowedRegionsJson = "[]";
        Notes = string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public TenantDataResidencyPolicy(
        TenantId tenantId,
        string allowedRegionsJson,
        string? notes,
        long updatedBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        AllowedRegionsJson = string.IsNullOrWhiteSpace(allowedRegionsJson) ? "[]" : allowedRegionsJson;
        Notes = notes ?? string.Empty;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string AllowedRegionsJson { get; private set; }

    [SugarColumn(Length = 512, IsNullable = false)]
    public string Notes { get; private set; }

    public long UpdatedBy { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string allowedRegionsJson, string? notes, long updatedBy)
    {
        AllowedRegionsJson = string.IsNullOrWhiteSpace(allowedRegionsJson) ? "[]" : allowedRegionsJson;
        Notes = notes ?? string.Empty;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
