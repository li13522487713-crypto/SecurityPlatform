using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 治理 M-G05-C1（S9）：组织实体（Coze "组织" 维度）。
///
/// 与 Tenant 的关系：一个 Tenant 可有多个 Organization；默认每个 Tenant 由 bootstrap 注入一个 "Default Org"，
/// 既有 Workspace 在 S10 阶段会回填 OrganizationId 指向 default。
///
/// Workspace.OrganizationId 当前为 nullable（向后兼容）；S10 后强制非空。
/// </summary>
[SugarTable("Organization")]
public sealed class Organization : TenantEntity
{
    public Organization()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        IsDefault = false;
        CreatedAt = DateTime.UtcNow;
    }

    public Organization(
        TenantId tenantId,
        string code,
        string name,
        string? description,
        bool isDefault,
        long createdBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        IsDefault = isDefault;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        UpdatedBy = createdBy;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Code { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 512, IsNullable = false)]
    public string Description { get; private set; }

    public bool IsDefault { get; private set; }

    public long CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public long UpdatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public void Update(string name, string? description, long updatedBy)
    {
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
