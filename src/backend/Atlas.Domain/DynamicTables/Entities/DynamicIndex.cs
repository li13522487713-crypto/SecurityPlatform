using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.DynamicTables.Entities;

public sealed class DynamicIndex : TenantEntity
{
    public DynamicIndex()
        : base(TenantId.Empty)
    {
        TableId = 0;
        Name = string.Empty;
        IsUnique = false;
        FieldsJson = "[]";
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
    }

    public DynamicIndex(
        TenantId tenantId,
        long tableId,
        string name,
        bool isUnique,
        string fieldsJson,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableId = tableId;
        Name = name;
        IsUnique = isUnique;
        FieldsJson = fieldsJson;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long TableId { get; private set; }
    public string Name { get; private set; }
    public bool IsUnique { get; private set; }
    public string FieldsJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string fieldsJson, bool isUnique, DateTimeOffset now)
    {
        FieldsJson = fieldsJson;
        IsUnique = isUnique;
        UpdatedAt = now;
    }
}
