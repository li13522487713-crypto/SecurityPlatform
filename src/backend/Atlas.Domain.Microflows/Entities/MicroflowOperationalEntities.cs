using SqlSugar;

namespace Atlas.Domain.Microflows.Entities;

[SugarTable("MicroflowReference")]
[SugarIndex("IX_MicroflowReference_TargetMicroflowId", nameof(TargetMicroflowId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowReference_SourceType", nameof(SourceType), OrderByType.Asc)]
[SugarIndex("IX_MicroflowReference_SourceId", nameof(SourceId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowReference_Active", nameof(Active), OrderByType.Asc)]
[SugarIndex("IX_MicroflowReference_ImpactLevel", nameof(ImpactLevel), OrderByType.Asc)]
public sealed class MicroflowReferenceEntity
{
    public MicroflowReferenceEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        TargetMicroflowId = string.Empty;
        SourceType = "unknown";
        SourceName = string.Empty;
        ReferenceKind = "unknown";
        ImpactLevel = "none";
        Active = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string TargetMicroflowId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(Length = 32)]
    public string SourceType { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? SourceId { get; set; }

    [SugarColumn(Length = 256)]
    public string SourceName { get; set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string? SourcePath { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? SourceVersion { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ReferencedVersion { get; set; }

    [SugarColumn(Length = 64)]
    public string ReferenceKind { get; set; }

    [SugarColumn(Length = 32)]
    public string ImpactLevel { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Description { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowRunSession")]
[SugarIndex("IX_MicroflowRunSession_ResourceId", nameof(ResourceId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunSession_Status", nameof(Status), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunSession_StartedAt", nameof(StartedAt), OrderByType.Desc)]
[SugarIndex("IX_MicroflowRunSession_CreatedBy", nameof(CreatedBy), OrderByType.Asc)]
public sealed class MicroflowRunSessionEntity
{
    public MicroflowRunSessionEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        ResourceId = string.Empty;
        Status = "idle";
        InputJson = "{}";
        StartedAt = DateTimeOffset.UtcNow;
        Mode = "testRun";
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string ResourceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? SchemaSnapshotId { get; set; }

    [SugarColumn(Length = 32)]
    public string Status { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string InputJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? OutputJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorJson { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? CreatedBy { get; set; }

    [SugarColumn(Length = 32)]
    public string Mode { get; set; }

    public int TraceFrameCount { get; set; }

    public int LogCount { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowRunTraceFrame")]
[SugarIndex("IX_MicroflowRunTraceFrame_RunId", nameof(RunId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunTraceFrame_Run_Sequence", nameof(RunId), OrderByType.Asc, nameof(Sequence), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunTraceFrame_ObjectId", nameof(ObjectId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunTraceFrame_ActionId", nameof(ActionId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunTraceFrame_Status", nameof(Status), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunTraceFrame_WorkspaceId", nameof(WorkspaceId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunTraceFrame_TenantId", nameof(TenantId), OrderByType.Asc)]
public sealed class MicroflowRunTraceFrameEntity
{
    public MicroflowRunTraceFrameEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        RunId = string.Empty;
        ObjectId = string.Empty;
        Status = "running";
        StartedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string RunId { get; set; }

    /// <summary>
    /// Workspace 与 trace frame 直接绑定，用于按 runId 读取时的越权校验。
    /// 旧数据可能为 NULL（迁移期），此时由 service 层根据 session 反查 resource 的 workspace。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    public int Sequence { get; set; }

    [SugarColumn(Length = 128)]
    public string ObjectId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ActionId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? CollectionId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? IncomingFlowId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? OutgoingFlowId { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? SelectedCaseValueJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? LoopIterationJson { get; set; }

    [SugarColumn(Length = 32)]
    public string Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public int DurationMs { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? InputJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? OutputJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? VariablesSnapshotJson { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Message { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowRunLog")]
[SugarIndex("IX_MicroflowRunLog_RunId", nameof(RunId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunLog_Timestamp", nameof(Timestamp), OrderByType.Desc)]
[SugarIndex("IX_MicroflowRunLog_Level", nameof(Level), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunLog_WorkspaceId", nameof(WorkspaceId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowRunLog_TenantId", nameof(TenantId), OrderByType.Asc)]
public sealed class MicroflowRunLogEntity
{
    public MicroflowRunLogEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        RunId = string.Empty;
        Timestamp = DateTimeOffset.UtcNow;
        Level = "info";
        Message = string.Empty;
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string RunId { get; set; }

    /// <summary>
    /// Workspace 与 log 直接绑定，用于按 runId 读取日志时的越权校验。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    [SugarColumn(Length = 32)]
    public string Level { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ObjectId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ActionId { get; set; }

    [SugarColumn(Length = 4000)]
    public string Message { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowMetadataCache")]
[SugarIndex("IX_MicroflowMetadataCache_WorkspaceId", nameof(WorkspaceId), OrderByType.Asc)]
[SugarIndex("IX_MicroflowMetadataCache_CatalogVersion", nameof(CatalogVersion), OrderByType.Asc)]
[SugarIndex("IX_MicroflowMetadataCache_UpdatedAt", nameof(UpdatedAt), OrderByType.Desc)]
public sealed class MicroflowMetadataCacheEntity
{
    public MicroflowMetadataCacheEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        CatalogVersion = "backend-skeleton";
        CatalogJson = "{}";
        UpdatedAt = DateTimeOffset.UtcNow;
        Source = "generated";
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkspaceId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(Length = 128)]
    public string CatalogVersion { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string CatalogJson { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? UpdatedBy { get; set; }

    [SugarColumn(Length = 32)]
    public string Source { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}

[SugarTable("MicroflowSchemaMigration")]
[SugarIndex("IX_MicroflowSchemaMigration_From_To", nameof(FromVersion), OrderByType.Asc, nameof(ToVersion), OrderByType.Asc)]
[SugarIndex("IX_MicroflowSchemaMigration_AppliedAt", nameof(AppliedAt), OrderByType.Desc)]
public sealed class MicroflowSchemaMigrationEntity
{
    public MicroflowSchemaMigrationEntity()
    {
        Id = Guid.NewGuid().ToString("N");
        FromVersion = string.Empty;
        ToVersion = string.Empty;
        Description = string.Empty;
        AppliedAt = DateTimeOffset.UtcNow;
        Status = "applied";
    }

    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string Id { get; set; }

    [SugarColumn(Length = 64)]
    public string FromVersion { get; set; }

    [SugarColumn(Length = 64)]
    public string ToVersion { get; set; }

    [SugarColumn(Length = 2000)]
    public string Description { get; set; }

    public DateTimeOffset AppliedAt { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? AppliedBy { get; set; }

    [SugarColumn(Length = 32)]
    public string Status { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ExtraJson { get; set; }
}
