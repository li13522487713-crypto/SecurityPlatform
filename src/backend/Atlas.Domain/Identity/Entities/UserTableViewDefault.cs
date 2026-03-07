using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

[SugarIndex(
    "UX_UserTableViewDefault_Tenant_User_Table",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(TableKey), OrderByType.Asc,
    true)]
public sealed class UserTableViewDefault : TenantEntity
{
    public UserTableViewDefault()
        : base(TenantId.Empty)
    {
        UserId = 0;
        TableKey = string.Empty;
        ViewId = 0;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
    }

    public UserTableViewDefault(
        TenantId tenantId,
        long userId,
        string tableKey,
        long viewId,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        TableKey = tableKey;
        ViewId = viewId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long UserId { get; private set; }
    public string TableKey { get; private set; }
    public long ViewId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateView(long viewId, DateTimeOffset now)
    {
        ViewId = viewId;
        UpdatedAt = now;
    }
}
