using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 外部部门 ↔ 外部成员的多对多关系镜像。
/// IsPrimary 表示主部门，便于审批人路由 / 数据权限继承时取主部门。
/// </summary>
public sealed class ExternalDepartmentUserRelation : TenantEntity
{
    public ExternalDepartmentUserRelation()
        : base(TenantId.Empty)
    {
        ExternalDepartmentId = string.Empty;
        ExternalUserId = string.Empty;
    }

    public ExternalDepartmentUserRelation(
        TenantId tenantId,
        long id,
        long providerId,
        string externalDepartmentId,
        string externalUserId,
        bool isPrimary,
        int order,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        ExternalDepartmentId = externalDepartmentId;
        ExternalUserId = externalUserId;
        IsPrimary = isPrimary;
        Order = order;
        FirstSeenAt = now;
        LastSyncedAt = now;
    }

    public long ProviderId { get; private set; }

    public string ExternalDepartmentId { get; private set; }

    public string ExternalUserId { get; private set; }

    public bool IsPrimary { get; private set; }

    public int Order { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSyncedAt { get; private set; }

    public void Refresh(bool isPrimary, int order, DateTimeOffset now)
    {
        IsPrimary = isPrimary;
        Order = order;
        LastSyncedAt = now;
    }
}
