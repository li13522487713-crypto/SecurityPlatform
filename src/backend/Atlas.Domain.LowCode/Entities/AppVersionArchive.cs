using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 应用版本归档（M01 落地，对应 docx §10.6 VersionArchive）。
/// 含完整 schema 快照 + 依赖资源版本快照 + 构建产物元数据 + 备注。
///
/// M14 将基于本聚合实现 diff / rollback 与端点双套校准（设计态 v1 + 运行时 runtime）；
/// M16 协同离线快照亦写入此聚合（区分 IsSystemSnapshot=true/false）。
/// </summary>
public sealed class AppVersionArchive : TenantEntity
{
#pragma warning disable CS8618
    public AppVersionArchive()
        : base(TenantId.Empty)
    {
        VersionLabel = string.Empty;
        SchemaSnapshotJson = "{}";
        ResourceSnapshotJson = "{}";
    }
#pragma warning restore CS8618

    public AppVersionArchive(
        TenantId tenantId,
        long id,
        long appId,
        string versionLabel,
        string schemaSnapshotJson,
        string resourceSnapshotJson,
        string? note,
        long createdByUserId,
        bool isSystemSnapshot)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        VersionLabel = versionLabel;
        SchemaSnapshotJson = string.IsNullOrWhiteSpace(schemaSnapshotJson) ? "{}" : schemaSnapshotJson;
        ResourceSnapshotJson = string.IsNullOrWhiteSpace(resourceSnapshotJson) ? "{}" : resourceSnapshotJson;
        Note = note;
        CreatedByUserId = createdByUserId;
        IsSystemSnapshot = isSystemSnapshot;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public long AppId { get; private set; }

    /// <summary>版本标签（如 v1.0.0 / 2026-04-17-001 / autosave-xxxx）。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string VersionLabel { get; private set; }

    /// <summary>schema 完整快照 JSON（AppSchema 全量）。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string SchemaSnapshotJson { get; private set; }

    /// <summary>
    /// 依赖资源版本快照 JSON（含工作流版本 / 对话流版本 / 知识库版本 / 数据库快照 / 变量快照 / 插件版本 / 提示词模板版本）。
    /// 完整字段在 M14 / M18 进一步细化。
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string ResourceSnapshotJson { get; private set; }

    /// <summary>构建产物元数据 JSON（指纹 / CDN URL / 渲染器矩阵），M17 PublishArtifact 关联。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? BuildMetadataJson { get; private set; }

    /// <summary>备注。</summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Note { get; private set; }

    /// <summary>创建者用户 ID（系统快照 = 0 占位）。</summary>
    public long CreatedByUserId { get; private set; }

    /// <summary>是否系统快照（M16 协同离线快照 = true，与用户主动版本区分）。</summary>
    public bool IsSystemSnapshot { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void AttachBuildMetadata(string buildMetadataJson)
    {
        BuildMetadataJson = buildMetadataJson;
    }
}
