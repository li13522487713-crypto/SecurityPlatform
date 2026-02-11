using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.LowCode.Entities;

public sealed class DataSourceDefinition : TenantEntity
{
    public DataSourceDefinition() : base(TenantId.Empty) { Name = string.Empty; SourceType = string.Empty; ConfigJson = string.Empty; }

    public DataSourceDefinition(TenantId tenantId, string name, string sourceType, string configJson, string? description, long createdBy, long id, DateTimeOffset now) : base(tenantId)
    {
        Id = id; Name = name; SourceType = sourceType; ConfigJson = configJson;
        Description = description; CreatedAt = now; UpdatedAt = now; CreatedBy = createdBy;
    }

    public string Name { get; private set; }
    public string SourceType { get; private set; }
    public string ConfigJson { get; private set; }
    public string? Description { get; private set; }
    public int? CacheSeconds { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long CreatedBy { get; private set; }

    public void Update(string name, string sourceType, string configJson, string? description, DateTimeOffset now)
    {
        Name = name; SourceType = sourceType; ConfigJson = configJson; Description = description; UpdatedAt = now;
    }

    public void SetCache(int? cacheSeconds, DateTimeOffset now)
    {
        CacheSeconds = cacheSeconds; UpdatedAt = now;
    }
}
