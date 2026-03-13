using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiDatabaseRecord : TenantEntity
{
    public AiDatabaseRecord()
        : base(TenantId.Empty)
    {
        DataJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public AiDatabaseRecord(
        TenantId tenantId,
        long databaseId,
        string dataJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        DatabaseId = databaseId;
        DataJson = string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatabaseId { get; private set; }
    public string DataJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void UpdateData(string dataJson)
    {
        DataJson = string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
