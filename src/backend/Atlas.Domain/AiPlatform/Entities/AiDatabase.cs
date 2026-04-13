using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiDatabase : TenantEntity
{
    public AiDatabase()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        TableSchema = "[]";
        OwnerType = AiDatabaseOwnerType.Library;
        CreatedAt = DateTime.UtcNow;
    }

    public AiDatabase(
        TenantId tenantId,
        string name,
        string? description,
        long? botId,
        string tableSchema,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        BotId = botId;
        OwnerType = botId.HasValue ? AiDatabaseOwnerType.Agent : AiDatabaseOwnerType.Library;
        OwnerId = botId;
        TableSchema = string.IsNullOrWhiteSpace(tableSchema) ? "[]" : tableSchema;
        SchemaVersion = 1;
        RecordCount = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public long? BotId { get; private set; }
    public AiDatabaseOwnerType OwnerType { get; private set; }
    public long? OwnerId { get; private set; }
    public string TableSchema { get; private set; }
    public int SchemaVersion { get; private set; }
    public int PublishedVersion { get; private set; }
    public int RecordCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        long? botId,
        string tableSchema,
        AiDatabaseOwnerType? ownerType = null,
        long? ownerId = null)
    {
        Name = name;
        Description = description ?? string.Empty;
        BotId = botId;
        OwnerType = ownerType ?? (botId.HasValue ? AiDatabaseOwnerType.Agent : OwnerType);
        OwnerId = ownerId ?? botId;
        TableSchema = string.IsNullOrWhiteSpace(tableSchema) ? "[]" : tableSchema;
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
        BotId = null;
        if (OwnerType == AiDatabaseOwnerType.Agent)
        {
            OwnerType = AiDatabaseOwnerType.Library;
            OwnerId = null;
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
}

public enum AiDatabaseOwnerType
{
    Library = 0,
    Agent = 1,
    App = 2
}
