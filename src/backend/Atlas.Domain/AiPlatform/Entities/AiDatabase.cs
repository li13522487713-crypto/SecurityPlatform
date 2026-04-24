using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

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
    public string TableSchema { get; private set; }
    public int SchemaVersion { get; private set; }
    public int PublishedVersion { get; private set; }
    public int RecordCount { get; private set; }
    /// <summary>D2：行可见性策略。SingleUser=按 OwnerUserId 过滤；MultiUser=不过滤。</summary>
    public AiDatabaseQueryMode QueryMode { get; private set; }
    /// <summary>D2：渠道隔离策略。支持完全共享 / 渠道隔离 / 站内共享。</summary>
    public AiDatabaseChannelScope ChannelScope { get; private set; }
    public string DraftTableName { get; private set; }
    public string OnlineTableName { get; private set; }
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
