using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// Mendix Studio Domain Model 文档。
/// 以 app + module 为粒度保存数据库绑定、实体映射、布局与最近一次同步状态。
/// </summary>
public sealed class MendixDomainModelDocument : TenantEntity
{
#pragma warning disable CS8618
    public MendixDomainModelDocument()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        ModuleId = string.Empty;
        DocumentJson = "{}";
        SyncStateJson = "{}";
    }
#pragma warning restore CS8618

    public MendixDomainModelDocument(
        TenantId tenantId,
        long id,
        long appId,
        string workspaceId,
        string moduleId,
        string documentJson,
        string syncStateJson,
        long? updatedByUserId)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        WorkspaceId = workspaceId;
        ModuleId = moduleId;
        DocumentJson = documentJson;
        SyncStateJson = syncStateJson;
        UpdatedByUserId = updatedByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AppId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string WorkspaceId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string ModuleId { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string DocumentJson { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string SyncStateJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long? UpdatedByUserId { get; private set; }

    public void ReplaceDocument(string documentJson, string syncStateJson, long? updatedByUserId)
    {
        DocumentJson = documentJson;
        SyncStateJson = syncStateJson;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
