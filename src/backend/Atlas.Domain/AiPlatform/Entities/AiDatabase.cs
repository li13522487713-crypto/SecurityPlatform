using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

#pragma warning disable CS0618 // 实体内部仍需维护旧字段的反序列化与兼容写入。
public sealed class AiDatabase : TenantEntity
{
    public AiDatabase()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        WorkspaceId = 0;
        BotId = 0;
        OwnerId = 0;
        TableSchema = "[]";
        OwnerType = AiDatabaseOwnerType.Library;
        QueryMode = AiDatabaseQueryMode.MultiUser;
        ChannelScope = AiDatabaseChannelScope.FullShared;
        DraftTableName = string.Empty;
        OnlineTableName = string.Empty;
        StorageMode = AiDatabaseStorageMode.Standalone;
        DriverCode = "SQLite";
        EncryptedDraftConnection = string.Empty;
        EncryptedOnlineConnection = string.Empty;
        PhysicalDatabaseName = string.Empty;
        DraftDatabaseName = string.Empty;
        OnlineDatabaseName = string.Empty;
        DefaultHostProfileId = null;
        DraftInstanceId = null;
        OnlineInstanceId = null;
        DialectVersion = "v1";
        ProvisionState = AiDatabaseProvisionState.Pending;
        ResourceSource = LibrarySource.Custom;
        CreatedAt = DateTime.UtcNow;
    }

    public AiDatabase(
        TenantId tenantId,
        string name,
        string? description,
        long? botId,
        string tableSchema,
        long id,
        long? workspaceId = null,
        AiDatabaseQueryMode? queryMode = null,
        AiDatabaseChannelScope? channelScope = null,
        LibrarySource resourceSource = LibrarySource.Custom)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        WorkspaceId = workspaceId ?? 0;
        Description = description ?? string.Empty;
        BotId = botId ?? 0;
        OwnerType = botId.HasValue ? AiDatabaseOwnerType.Agent : AiDatabaseOwnerType.Library;
        OwnerId = botId ?? 0;
        TableSchema = string.IsNullOrWhiteSpace(tableSchema) ? "[]" : tableSchema;
        SchemaVersion = 1;
        RecordCount = 0;
        QueryMode = queryMode ?? AiDatabaseQueryMode.MultiUser;
        ChannelScope = channelScope ?? AiDatabaseChannelScope.FullShared;
        DraftTableName = string.Empty;
        OnlineTableName = string.Empty;
        StorageMode = AiDatabaseStorageMode.Standalone;
        DriverCode = "SQLite";
        EncryptedDraftConnection = string.Empty;
        EncryptedOnlineConnection = string.Empty;
        PhysicalDatabaseName = string.Empty;
        DraftDatabaseName = string.Empty;
        OnlineDatabaseName = string.Empty;
        DefaultHostProfileId = null;
        DraftInstanceId = null;
        OnlineInstanceId = null;
        DialectVersion = "v1";
        ProvisionState = AiDatabaseProvisionState.Pending;
        ResourceSource = resourceSource;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public long? WorkspaceId { get; private set; }
    public string? Description { get; private set; }
    public long? BotId { get; private set; }
    public AiDatabaseOwnerType OwnerType { get; private set; }
    public long? OwnerId { get; private set; }
    [Obsolete("旧 JSON 行模型字段，仅用于兼容读取；新结构管理不得使用。")]
    public string TableSchema { get; private set; }
    [Obsolete("旧 JSON 行模型字段，仅用于兼容读取；新结构管理不得使用。")]
    public int SchemaVersion { get; private set; }
    [Obsolete("旧 JSON 行模型字段，仅用于兼容读取；新结构管理不得使用。")]
    public int PublishedVersion { get; private set; }
    [Obsolete("旧 JSON 行模型字段，仅用于兼容读取；新结构管理行数从物理库读取。")]
    public int RecordCount { get; private set; }
    /// <summary>D2：行可见性策略。SingleUser=按 OwnerUserId 过滤；MultiUser=不过滤。</summary>
    public AiDatabaseQueryMode QueryMode { get; private set; }
    /// <summary>D2：渠道隔离策略。支持完全共享 / 渠道隔离 / 站内共享。</summary>
    public AiDatabaseChannelScope ChannelScope { get; private set; }
    [Obsolete("旧主库物理表字段，仅用于兼容读取；新结构管理不得使用。")]
    public string DraftTableName { get; private set; }
    [Obsolete("旧主库物理表字段，仅用于兼容读取；新结构管理不得使用。")]
    public string OnlineTableName { get; private set; }
    public AiDatabaseStorageMode StorageMode { get; private set; }
    public string DriverCode { get; private set; }
    [SugarColumn(Length = 4096)]
    public string EncryptedDraftConnection { get; private set; }
    [SugarColumn(Length = 4096)]
    public string EncryptedOnlineConnection { get; private set; }
    public string PhysicalDatabaseName { get; private set; }
    public string DraftDatabaseName { get; private set; }
    public string OnlineDatabaseName { get; private set; }
    public long? DefaultHostProfileId { get; private set; }
    public long? DraftInstanceId { get; private set; }
    public long? OnlineInstanceId { get; private set; }
    public string DialectVersion { get; private set; }
    public AiDatabaseProvisionState ProvisionState { get; private set; }
    public string? ProvisionError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    [SugarColumn(IsNullable = false)]
    public LibrarySource ResourceSource { get; private set; }

    public void SetQueryMode(AiDatabaseQueryMode queryMode, AiDatabaseChannelScope channelScope)
    {
        QueryMode = queryMode;
        ChannelScope = channelScope;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPhysicalTables(string draftTableName, string onlineTableName)
    {
        DraftTableName = draftTableName?.Trim() ?? string.Empty;
        OnlineTableName = onlineTableName?.Trim() ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public string GetTableName(AiDatabaseRecordEnvironment environment)
        => environment == AiDatabaseRecordEnvironment.Online ? OnlineTableName : DraftTableName;

    public void ConfigureStandaloneStorage(
        string driverCode,
        string encryptedDraftConnection,
        string encryptedOnlineConnection,
        string physicalDatabaseName)
        => ConfigureStandaloneStorage(
            driverCode,
            encryptedDraftConnection,
            encryptedOnlineConnection,
            physicalDatabaseName,
            $"{physicalDatabaseName}_draft",
            $"{physicalDatabaseName}_online",
            "v1");

    public void ConfigureStandaloneStorage(
        string driverCode,
        string encryptedDraftConnection,
        string encryptedOnlineConnection,
        string physicalDatabaseName,
        string draftDatabaseName,
        string onlineDatabaseName,
        string dialectVersion)
    {
        StorageMode = AiDatabaseStorageMode.Standalone;
        DriverCode = string.IsNullOrWhiteSpace(driverCode) ? "SQLite" : driverCode.Trim();
        EncryptedDraftConnection = encryptedDraftConnection?.Trim() ?? string.Empty;
        EncryptedOnlineConnection = encryptedOnlineConnection?.Trim() ?? string.Empty;
        PhysicalDatabaseName = physicalDatabaseName?.Trim() ?? string.Empty;
        DraftDatabaseName = draftDatabaseName?.Trim() ?? string.Empty;
        OnlineDatabaseName = onlineDatabaseName?.Trim() ?? string.Empty;
        DialectVersion = string.IsNullOrWhiteSpace(dialectVersion) ? "v1" : dialectVersion.Trim();
        ProvisionState = AiDatabaseProvisionState.Ready;
        ProvisionError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStandaloneDriver(string driverCode)
    {
        StorageMode = AiDatabaseStorageMode.Standalone;
        DriverCode = string.IsNullOrWhiteSpace(driverCode) ? "SQLite" : driverCode.Trim();
        ProvisionState = AiDatabaseProvisionState.Pending;
        ProvisionError = null;
        DialectVersion = string.IsNullOrWhiteSpace(DialectVersion) ? "v1" : DialectVersion;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfigureManagedInstances(
        string driverCode,
        long hostProfileId,
        long? draftInstanceId,
        long? onlineInstanceId,
        string? draftDatabaseName,
        string? onlineDatabaseName)
    {
        StorageMode = AiDatabaseStorageMode.Standalone;
        DriverCode = string.IsNullOrWhiteSpace(driverCode) ? "SQLite" : driverCode.Trim();
        DefaultHostProfileId = hostProfileId;
        DraftInstanceId = draftInstanceId;
        OnlineInstanceId = onlineInstanceId;
        DraftDatabaseName = draftDatabaseName?.Trim() ?? string.Empty;
        OnlineDatabaseName = onlineDatabaseName?.Trim() ?? string.Empty;
        ProvisionState = AiDatabaseProvisionState.Pending;
        ProvisionError = null;
        DialectVersion = string.IsNullOrWhiteSpace(DialectVersion) ? "v1" : DialectVersion;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkProvisionReady()
    {
        ProvisionState = AiDatabaseProvisionState.Ready;
        ProvisionError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkProvisionPending()
    {
        ProvisionState = AiDatabaseProvisionState.Pending;
        ProvisionError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkProvisionFailed(string error)
    {
        ProvisionState = AiDatabaseProvisionState.Failed;
        ProvisionError = string.IsNullOrWhiteSpace(error) ? "Provision failed." : error.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string? description,
        long? botId,
        string tableSchema,
        AiDatabaseQueryMode? queryMode = null,
        AiDatabaseChannelScope? channelScope = null,
        AiDatabaseOwnerType? ownerType = null,
        long? ownerId = null,
        long? workspaceId = null)
    {
        Name = name;
        if (workspaceId.HasValue)
        {
            WorkspaceId = workspaceId.Value;
        }
        Description = description ?? string.Empty;
        BotId = botId ?? 0;
        OwnerType = ownerType ?? (botId.HasValue ? AiDatabaseOwnerType.Agent : OwnerType);
        OwnerId = ownerId ?? botId ?? 0;
        TableSchema = string.IsNullOrWhiteSpace(tableSchema) ? "[]" : tableSchema;
        QueryMode = queryMode ?? QueryMode;
        ChannelScope = channelScope ?? ChannelScope;
        SchemaVersion = Math.Max(1, SchemaVersion + 1);
        UpdatedAt = DateTime.UtcNow;
    }

    public void BindBot(long botId)
    {
        BotId = botId;
        OwnerType = AiDatabaseOwnerType.Agent;
        OwnerId = botId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnbindBot()
    {
        BotId = 0;
        if (OwnerType == AiDatabaseOwnerType.Agent)
        {
            OwnerType = AiDatabaseOwnerType.Library;
            OwnerId = 0;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRecordCount(int count)
    {
        RecordCount = Math.Max(0, count);
        UpdatedAt = DateTime.UtcNow;
    }

    public void PublishSchema()
    {
        PublishedVersion++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignWorkspace(long workspaceId)
    {
        if (workspaceId <= 0)
        {
            return;
        }

        WorkspaceId = workspaceId;
        UpdatedAt = DateTime.UtcNow;
    }
}
#pragma warning restore CS0618

public enum AiDatabaseOwnerType
{
    Library = 0,
    Agent = 1,
    App = 2
}

public enum AiDatabaseQueryMode
{
    /// <summary>不限定 owner 过滤；老数据兼容默认。</summary>
    MultiUser = 0,
    /// <summary>读写仅限 OwnerUserId 等于当前用户或 NULL（旧数据）。</summary>
    SingleUser = 1
}

public enum AiDatabaseChannelScope
{
    /// <summary>所有渠道共享数据。</summary>
    FullShared = 0,
    /// <summary>严格按当前渠道隔离。</summary>
    ChannelIsolated = 1,
    /// <summary>站内渠道共享，其他渠道彼此隔离。</summary>
    InternalShared = 2
}

public enum AiDatabaseRecordEnvironment
{
    Draft = 1,
    Online = 2
}

public enum AiDatabaseStorageMode
{
    LegacyJson = 0,
    Standalone = 1
}

public enum AiDatabaseProvisionState
{
    Pending = 0,
    Ready = 1,
    Failed = 2
}
