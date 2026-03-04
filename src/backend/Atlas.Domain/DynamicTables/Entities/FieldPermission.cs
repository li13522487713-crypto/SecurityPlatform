using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 动态表字段级权限（按角色定义可见/可编辑）。
/// </summary>
public sealed class FieldPermission : TenantEntity
{
    public FieldPermission()
        : base(TenantId.Empty)
    {
        TableKey = string.Empty;
        FieldName = string.Empty;
        RoleCode = string.Empty;
    }

    public FieldPermission(
        TenantId tenantId,
        string tableKey,
        string fieldName,
        string roleCode,
        bool canView,
        bool canEdit,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableKey = tableKey;
        FieldName = fieldName;
        RoleCode = roleCode;
        CanView = canView;
        CanEdit = canEdit;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string TableKey { get; private set; }
    public string FieldName { get; private set; }
    public string RoleCode { get; private set; }
    public bool CanView { get; private set; }
    public bool CanEdit { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(bool canView, bool canEdit, DateTimeOffset now)
    {
        CanView = canView;
        CanEdit = canEdit;
        UpdatedAt = now;
    }
}
