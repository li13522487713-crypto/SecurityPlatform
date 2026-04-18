using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 镜像 → 本地组织树的可控映射。一个外部部门可以映射到 0 或 1 个本地 Department。
/// 不映射时仅作为目录可见而不进入 RBAC 视图。
/// </summary>
public sealed class LocalDepartmentMapping : TenantEntity
{
    public LocalDepartmentMapping()
        : base(TenantId.Empty)
    {
        ExternalDepartmentId = string.Empty;
    }

    public LocalDepartmentMapping(
        TenantId tenantId,
        long id,
        long providerId,
        string externalDepartmentId,
        long? localDepartmentId,
        bool autoSyncMembers,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        ExternalDepartmentId = externalDepartmentId;
        LocalDepartmentId = localDepartmentId;
        AutoSyncMembers = autoSyncMembers;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long ProviderId { get; private set; }

    public string ExternalDepartmentId { get; private set; }

    /// <summary>本地 Department.Id；null 表示该外部部门暂不接入本地 RBAC。</summary>
    public long? LocalDepartmentId { get; private set; }

    public bool AutoSyncMembers { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void RebindLocal(long? localDepartmentId, DateTimeOffset now)
    {
        LocalDepartmentId = localDepartmentId;
        UpdatedAt = now;
    }

    public void SetAutoSync(bool autoSync, DateTimeOffset now)
    {
        AutoSyncMembers = autoSync;
        UpdatedAt = now;
    }
}
