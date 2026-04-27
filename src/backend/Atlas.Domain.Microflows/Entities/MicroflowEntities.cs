using SqlSugar;

namespace Atlas.Domain.Microflows.Entities;

[SugarTable("MicroflowResource")]
[SugarIndex("IX_MicroflowResource_WorkspaceId", nameof(WorkspaceId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowResource_TenantId", nameof(TenantId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowResource_ModuleId", nameof(ModuleId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowResource_Status", nameof(Status), OrderByType.Asc)]
[SugarIndex("IX_MicroflowResource_PublishStatus", nameof(PublishStatus), OrderByType.Asc)]
[SugarIndex("IX_MicroflowResource_UpdatedAt", nameof(UpdatedAt), OrderByType.Desc)]
[SugarIndex("IX_MicroflowResource_Name", nameof(Name), OrderByType.Asc)]
[SugarIndex("IX_MicroflowResource_Archived", nameof(Archived), OrderByType.Asc)]
[SugarIndex("IX_MicroflowResource_Favorite", nameof(Favorite), OrderByType.Asc)]
[SugarIndex("UX_MicroflowResource_Workspace_Name", nameof(WorkspaceId), OrderByType.Asc, nameof(Name), OrderByType.Asc, true)]
public sealed class MicroflowResourceEntity
{
    public MicroflowResourceEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        ModuleId = string.Empty;
        Name = string.Empty;
        DisplayName = string.Empty;
        TagsJson = "[]";
        Version = "0.1.0";
        Status = "draft";
        PublishStatus = "neverPublished";
        LastRunStatus = "neverRun";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(Length = 128)]
    public string ModuleId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ModuleName { get; set; }

    [SugarColumn(Length = 128)]
    public string Name { get; set; }

    [SugarColumn(Length = 256)]
    public string DisplayName { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string TagsJson { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? OwnerId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? OwnerName { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [SugarColumn(Length = 64)]
    public string Version { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? LatestPublishedVersion { get; set; }

    [SugarColumn(Length = 32)]
    public string Status { get; set; }

    [SugarColumn(Length = 32)]
    public string PublishStatus { get; set; }

    public bool Favorite { get; set; }

    public bool Archived { get; set; }

    public int ReferenceCount { get; set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? LastRunStatus { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? SchemaId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? CurrentSchemaSnapshotId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ConcurrencyStamp { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowSchemaSnapshot")]
[SugarIndex("IX_MicroflowSchemaSnapshot_ResourceId", nameof(ResourceId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowSchemaSnapshot_CreatedAt", nameof(CreatedAt), OrderByType.Desc)]
public sealed class MicroflowSchemaSnapshotEntity
{
    public MicroflowSchemaSnapshotEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        ResourceId = string.Empty;
        SchemaVersion = "1.0";
        SchemaJson = "{}";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string ResourceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(Length = 64)]
    public string SchemaVersion { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? MigrationVersion { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string SchemaJson { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? SchemaHash { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string? Reason { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? BaseVersion { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowVersion")]
[SugarIndex("IX_MicroflowVersion_ResourceId", nameof(ResourceId), OrderByType.Asc)]
[SugarIndex("UX_MicroflowVersion_Resource_Version", nameof(ResourceId), OrderByType.Asc, nameof(Version), OrderByType.Asc, true)]
[SugarIndex("IX_MicroflowVersion_IsLatestPublished", nameof(IsLatestPublished), OrderByType.Asc)]
[SugarIndex("IX_MicroflowVersion_CreatedAt", nameof(CreatedAt), OrderByType.Desc)]
public sealed class MicroflowVersionEntity
{
    public MicroflowVersionEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        ResourceId = string.Empty;
        Version = "0.1.0";
        Status = "draft";
        SchemaSnapshotId = string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string ResourceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(Length = 64)]
    public string Version { get; set; }

    [SugarColumn(Length = 32)]
    public string Status { get; set; }

    [SugarColumn(Length = 64)]
    public string SchemaSnapshotId { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ValidationSummaryJson { get; set; }

    public int ReferenceCount { get; set; }

    public bool IsLatestPublished { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowPublishSnapshot")]
[SugarIndex("IX_MicroflowPublishSnapshot_Resource_Version", nameof(ResourceId), OrderByType.Asc, nameof(Version), OrderByType.Asc)]
[SugarIndex("IX_MicroflowPublishSnapshot_PublishedAt", nameof(PublishedAt), OrderByType.Desc)]
public sealed class MicroflowPublishSnapshotEntity
{
    public MicroflowPublishSnapshotEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        ResourceId = string.Empty;
        Version = string.Empty;
        SchemaSnapshotId = string.Empty;
        SchemaJson = "{}";
        ValidationSummaryJson = "{}";
        PublishedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string ResourceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(Length = 64)]
    public string Version { get; set; }

    [SugarColumn(Length = 64)]
    public string SchemaSnapshotId { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string SchemaJson { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string ValidationSummaryJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ImpactAnalysisJson { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? PublishedBy { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? SchemaHash { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}
