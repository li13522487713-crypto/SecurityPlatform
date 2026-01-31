using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public sealed class TableView : TenantEntity
{
    public TableView()
        : base(TenantId.Empty)
    {
        UserId = 0;
        TableKey = string.Empty;
        Name = string.Empty;
        ConfigJson = "{}";
        ConfigVersion = 1;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
        LastUsedAt = null;
    }

    public TableView(
        TenantId tenantId,
        long userId,
        string tableKey,
        string name,
        string configJson,
        int configVersion,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        TableKey = tableKey;
        Name = name;
        ConfigJson = configJson;
        ConfigVersion = configVersion;
        CreatedAt = now;
        UpdatedAt = now;
        LastUsedAt = now;
    }

    public long UserId { get; private set; }
    public string TableKey { get; private set; }
    public string Name { get; private set; }
    public string ConfigJson { get; private set; }
    public int ConfigVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public void Update(string name, string configJson, int configVersion, DateTimeOffset now)
    {
        Name = name;
        ConfigJson = configJson;
        ConfigVersion = configVersion;
        UpdatedAt = now;
    }

    public void UpdateConfig(string configJson, int configVersion, DateTimeOffset now)
    {
        ConfigJson = configJson;
        ConfigVersion = configVersion;
        UpdatedAt = now;
    }

    public void TouchUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
    }
}
