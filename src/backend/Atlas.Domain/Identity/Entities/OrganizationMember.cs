using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 治理 M-G05-C4（S10）：组织成员（user 与组织的关联，独立于 workspace 成员）。
/// 角色编码 (RoleCode) 映射到组织级角色策略，由调用方约定（owner / admin / member 等）。
/// </summary>
[SugarTable("OrganizationMember")]
public sealed class OrganizationMember : TenantEntity
{
    public OrganizationMember()
        : base(TenantId.Empty)
    {
        RoleCode = "member";
        JoinedAt = DateTime.UtcNow;
    }

    public OrganizationMember(
        TenantId tenantId,
        long organizationId,
        long userId,
        string roleCode,
        long addedBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        RoleCode = string.IsNullOrWhiteSpace(roleCode) ? "member" : roleCode.Trim();
        AddedBy = addedBy;
        JoinedAt = DateTime.UtcNow;
        UpdatedAt = JoinedAt;
    }

    public long OrganizationId { get; private set; }
    public long UserId { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string RoleCode { get; private set; }

    public long AddedBy { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void ChangeRole(string roleCode)
    {
        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            RoleCode = roleCode.Trim();
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
