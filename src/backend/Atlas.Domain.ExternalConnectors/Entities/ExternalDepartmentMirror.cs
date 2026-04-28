using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 外部部门镜像。事实源直存，不做合并；本地组织树由 LocalDepartmentMapping 单独维护。
/// 唯一键：(TenantId, ProviderId, ExternalDepartmentId)。
/// </summary>
public sealed class ExternalDepartmentMirror : TenantEntity
{
    public ExternalDepartmentMirror()
        : base(TenantId.Empty)
    {
        ExternalDepartmentId = string.Empty;
        Name = string.Empty;
    }

    public ExternalDepartmentMirror(
        TenantId tenantId,
        long id,
        long providerId,
        string externalDepartmentId,
        string? parentExternalDepartmentId,
        string name,
        string? fullPath,
        int order,
        string? rawJson,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        ExternalDepartmentId = externalDepartmentId;
        ParentExternalDepartmentId = parentExternalDepartmentId;
        Name = name;
        FullPath = fullPath;
        Order = order;
        RawJson = rawJson;
        IsDeleted = false;
        FirstSeenAt = now;
        LastSyncedAt = now;
    }

    public long ProviderId { get; private set; }

    public string ExternalDepartmentId { get; private set; }

    public string? ParentExternalDepartmentId { get; private set; }

    public string Name { get; private set; }

    public string? FullPath { get; private set; }

    public int Order { get; private set; }

    public string? RawJson { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSyncedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public void UpdateFrom(string? parentId, string name, string? fullPath, int order, string? rawJson, DateTimeOffset now)
    {
        ParentExternalDepartmentId = parentId;
        Name = name;
        FullPath = fullPath;
        Order = order;
        RawJson = rawJson;
        IsDeleted = false;
        DeletedAt = null;
        LastSyncedAt = now;
    }

    public void MarkDeleted(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
        LastSyncedAt = now;
    }
}
