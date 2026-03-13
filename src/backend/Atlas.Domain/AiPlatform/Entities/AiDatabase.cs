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
        TableSchema = string.IsNullOrWhiteSpace(tableSchema) ? "[]" : tableSchema;
        RecordCount = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public long? BotId { get; private set; }
    public string TableSchema { get; private set; }
    public int RecordCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(string name, string? description, long? botId, string tableSchema)
    {
        Name = name;
        Description = description ?? string.Empty;
        BotId = botId;
        TableSchema = string.IsNullOrWhiteSpace(tableSchema) ? "[]" : tableSchema;
        UpdatedAt = DateTime.UtcNow;
    }

    public void BindBot(long botId)
    {
        BotId = botId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnbindBot()
    {
        BotId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRecordCount(int count)
    {
        RecordCount = Math.Max(0, count);
        UpdatedAt = DateTime.UtcNow;
    }
}
