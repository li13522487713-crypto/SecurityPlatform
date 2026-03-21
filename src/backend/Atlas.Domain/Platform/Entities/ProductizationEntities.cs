using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Platform.Entities;

public enum AppManifestStatus
{
    Draft = 0,
    Published = 1,
    Disabled = 2,
    Archived = 3
}

public enum TenantApplicationStatus
{
    Provisioning = 0,
    Active = 1,
    Disabled = 2,
    Archived = 3
}

public enum AppReleaseStatus
{
    Pending = 0,
    Released = 1,
    RolledBack = 2
}

public enum PackageArtifactType
{
    Structure = 0,
    Data = 1,
    Full = 2
}

public enum PackageArtifactStatus
{
    Exported = 0,
    Imported = 1,
    Failed = 2
}

public enum LicenseGrantMode
{
    Online = 0,
    Offline = 1
}

public enum ToolAuthorizationPolicyType
{
    Allow = 0,
    Deny = 1,
    RequireApproval = 2
}

public sealed class AppManifest : TenantEntity
{
    public AppManifest()
        : base(TenantId.Empty)
    {
        AppKey = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        Icon = string.Empty;
        ConfigJson = "{}";
    }

    public AppManifest(
        TenantId tenantId,
        long id,
        string appKey,
        string name,
        long createdBy,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppKey = appKey;
        Name = name;
        Description = string.Empty;
        Category = string.Empty;
        Icon = string.Empty;
        ConfigJson = "{}";
        Version = 1;
        Status = AppManifestStatus.Draft;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string AppKey { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Category { get; private set; }
    public string Icon { get; private set; }
    public string ConfigJson { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? DataSourceId { get; private set; }
    public int Version { get; private set; }
    public AppManifestStatus Status { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? PublishedBy { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? PublishedAt { get; private set; }

    public void Update(string name, string? description, string? category, string? icon, long? dataSourceId, long updatedBy, DateTimeOffset now)
    {
        Name = name;
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        Icon = icon ?? string.Empty;
        DataSourceId = dataSourceId;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Publish(long updatedBy, DateTimeOffset now)
    {
        Version += 1;
        Status = AppManifestStatus.Published;
        PublishedBy = updatedBy;
        PublishedAt = now;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Archive(long updatedBy, DateTimeOffset now)
    {
        Status = AppManifestStatus.Archived;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void SyncReleaseVersion(int version, long updatedBy, DateTimeOffset now)
    {
        Version = version < 1 ? 1 : version;
        Status = AppManifestStatus.Published;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}

public sealed class TenantApplication : TenantEntity
{
    public TenantApplication()
        : base(TenantId.Empty)
    {
        AppKey = string.Empty;
        Name = string.Empty;
    }

    public TenantApplication(
        TenantId tenantId,
        long id,
        long catalogId,
        long appInstanceId,
        string appKey,
        string name,
        long? dataSourceId,
        long openedBy,
        DateTimeOffset openedAt)
        : base(tenantId)
    {
        Id = id;
        CatalogId = catalogId;
        AppInstanceId = appInstanceId;
        AppKey = appKey;
        Name = name;
        DataSourceId = dataSourceId;
        Status = TenantApplicationStatus.Active;
        OpenedBy = openedBy;
        OpenedAt = openedAt;
        UpdatedBy = openedBy;
        UpdatedAt = openedAt;
    }

    public long CatalogId { get; private set; }
    public long AppInstanceId { get; private set; }
    public string AppKey { get; private set; }
    public string Name { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? DataSourceId { get; private set; }
    public TenantApplicationStatus Status { get; private set; }
    public long OpenedBy { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void SyncWithInstance(
        long catalogId,
        long appInstanceId,
        string appKey,
        string name,
        long? dataSourceId,
        TenantApplicationStatus status,
        long updatedBy,
        DateTimeOffset now)
    {
        CatalogId = catalogId;
        AppInstanceId = appInstanceId;
        AppKey = appKey;
        Name = name;
        DataSourceId = dataSourceId;
        Status = status;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Disable(long updatedBy, DateTimeOffset now)
    {
        Status = TenantApplicationStatus.Disabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Enable(long updatedBy, DateTimeOffset now)
    {
        Status = TenantApplicationStatus.Active;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}

public sealed class AppRelease : TenantEntity
{
    public AppRelease()
        : base(TenantId.Empty)
    {
        ReleaseNote = string.Empty;
        SnapshotJson = "{}";
    }

    public AppRelease(
        TenantId tenantId,
        long id,
        long manifestId,
        int version,
        string snapshotJson,
        long releasedBy,
        DateTimeOffset releasedAt)
        : base(tenantId)
    {
        Id = id;
        ManifestId = manifestId;
        Version = version;
        SnapshotJson = snapshotJson;
        ReleaseNote = string.Empty;
        Status = AppReleaseStatus.Released;
        ReleasedBy = releasedBy;
        ReleasedAt = releasedAt;
    }

    public long ManifestId { get; private set; }
    public int Version { get; private set; }
    public string ReleaseNote { get; private set; }
    public string SnapshotJson { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? RollbackPointId { get; private set; }
    public AppReleaseStatus Status { get; private set; }
    public long ReleasedBy { get; private set; }
    public DateTimeOffset ReleasedAt { get; private set; }

    public void MarkRolledBack(long rollbackPointId)
    {
        RollbackPointId = rollbackPointId;
        Status = AppReleaseStatus.RolledBack;
    }

    public void MarkReleased(string? releaseNote = null)
    {
        Status = AppReleaseStatus.Released;
        RollbackPointId = null;
        if (!string.IsNullOrWhiteSpace(releaseNote))
        {
            ReleaseNote = releaseNote.Trim();
        }
    }
}

public sealed class RuntimeRoute : TenantEntity
{
    public RuntimeRoute()
        : base(TenantId.Empty)
    {
        AppKey = string.Empty;
        PageKey = string.Empty;
        EnvironmentCode = string.Empty;
    }

    public RuntimeRoute(
        TenantId tenantId,
        long id,
        long manifestId,
        string appKey,
        string pageKey,
        int schemaVersion)
        : base(tenantId)
    {
        Id = id;
        ManifestId = manifestId;
        AppKey = appKey;
        PageKey = pageKey;
        SchemaVersion = schemaVersion;
        IsActive = true;
        EnvironmentCode = "prod";
    }

    public long ManifestId { get; private set; }
    public string AppKey { get; private set; }
    public string PageKey { get; private set; }
    public int SchemaVersion { get; private set; }
    public bool IsActive { get; private set; }
    public string EnvironmentCode { get; private set; }

    public void Disable() => IsActive = false;

    public void Activate(int schemaVersion, string? environmentCode = null)
    {
        SchemaVersion = schemaVersion;
        IsActive = true;
        if (!string.IsNullOrWhiteSpace(environmentCode))
        {
            EnvironmentCode = environmentCode.Trim();
        }
    }

    public void RebindManifest(long manifestId)
    {
        ManifestId = manifestId;
    }
}

public sealed class PackageArtifact : TenantEntity
{
    public PackageArtifact()
        : base(TenantId.Empty)
    {
        FilePath = string.Empty;
        FileHash = string.Empty;
    }

    public PackageArtifact(
        TenantId tenantId,
        long id,
        long manifestId,
        PackageArtifactType packageType,
        string filePath,
        string fileHash,
        long size,
        long operatorUserId,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ManifestId = manifestId;
        PackageType = packageType;
        FilePath = filePath;
        FileHash = fileHash;
        Size = size;
        Status = PackageArtifactStatus.Exported;
        ExportedBy = operatorUserId;
        ExportedAt = now;
        // 兼容历史 SQLite 表结构中 ImportedBy/ImportedAt 的 NOT NULL 约束，
        // 避免导出阶段插入失败（状态仍以 Status=Exported 表示“尚未导入”）。
        ImportedBy = 0;
        ImportedAt = now;
    }

    public long ManifestId { get; private set; }
    public PackageArtifactType PackageType { get; private set; }
    public string FilePath { get; private set; }
    public string FileHash { get; private set; }
    public long Size { get; private set; }
    public PackageArtifactStatus Status { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? ExportedBy { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? ExportedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? ImportedBy { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? ImportedAt { get; private set; }

    public void MarkImported(long userId, DateTimeOffset now)
    {
        ImportedBy = userId;
        ImportedAt = now;
        Status = PackageArtifactStatus.Imported;
    }
}

public sealed class LicenseGrant : EntityBase
{
    public LicenseGrant()
    {
        OfflineRequestToken = string.Empty;
        FeaturesJson = "{}";
        LimitsJson = "{}";
        AuditTrailJson = "[]";
    }

    public LicenseGrant(
        long id,
        string offlineRequestToken,
        LicenseGrantMode grantMode,
        string featuresJson,
        string limitsJson,
        DateTimeOffset issuedAt)
    {
        SetId(id);
        OfflineRequestToken = offlineRequestToken;
        GrantMode = grantMode;
        FeaturesJson = featuresJson;
        LimitsJson = limitsJson;
        RenewalCount = 0;
        IssuedAt = issuedAt;
        AuditTrailJson = "[]";
    }

    public string OfflineRequestToken { get; private set; }
    public LicenseGrantMode GrantMode { get; private set; }
    public int RenewalCount { get; private set; }
    public string FeaturesJson { get; private set; }
    public string LimitsJson { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string AuditTrailJson { get; private set; }

    public void Renew(DateTimeOffset? expiresAt)
    {
        RenewalCount += 1;
        ExpiresAt = expiresAt;
    }
}

public sealed class ToolAuthorizationPolicy : TenantEntity
{
    public ToolAuthorizationPolicy()
        : base(TenantId.Empty)
    {
        ToolId = string.Empty;
        ToolName = string.Empty;
        ConditionJson = "{}";
    }

    public ToolAuthorizationPolicy(
        TenantId tenantId,
        long id,
        string toolId,
        string toolName,
        ToolAuthorizationPolicyType policyType,
        long createdBy,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ToolId = toolId;
        ToolName = toolName;
        PolicyType = policyType;
        ConditionJson = "{}";
        RateLimitQuota = 0;
        AuditEnabled = true;
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedBy = createdBy;
        UpdatedAt = now;
    }

    public string ToolId { get; private set; }
    public string ToolName { get; private set; }
    public ToolAuthorizationPolicyType PolicyType { get; private set; }
    public int RateLimitQuota { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? ApprovalFlowId { get; private set; }
    public string ConditionJson { get; private set; }
    public bool AuditEnabled { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdatePolicy(
        ToolAuthorizationPolicyType policyType,
        int rateLimitQuota,
        long? approvalFlowId,
        string? conditionJson,
        bool auditEnabled,
        long updatedBy,
        DateTimeOffset now)
    {
        PolicyType = policyType;
        RateLimitQuota = rateLimitQuota;
        ApprovalFlowId = approvalFlowId;
        ConditionJson = conditionJson ?? "{}";
        AuditEnabled = auditEnabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}

public sealed class AppDesignerSnapshot : TenantEntity
{
    public AppDesignerSnapshot()
        : base(TenantId.Empty)
    {
        SnapshotType = string.Empty;
        SchemaJson = "{}";
        CreatedBy = string.Empty;
    }

    public AppDesignerSnapshot(
        TenantId tenantId,
        long id,
        long manifestId,
        string snapshotType,
        long itemId,
        string schemaJson,
        int version,
        string createdBy,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        ManifestId = manifestId;
        SnapshotType = snapshotType;
        ItemId = itemId;
        SchemaJson = schemaJson;
        Version = version;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public long ManifestId { get; private set; }
    public string SnapshotType { get; private set; }
    public long ItemId { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string SchemaJson { get; private set; }
    public int Version { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
