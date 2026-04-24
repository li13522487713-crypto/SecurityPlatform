using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

[SugarTable("AiDatabaseField")]
public sealed class AiDatabaseField : TenantEntity
{
    public AiDatabaseField()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        FieldType = "string";
    }

    public AiDatabaseField(
        TenantId tenantId,
        long databaseId,
        string name,
        string? description,
        string fieldType,
        bool required,
        bool isSystemField,
        bool indexed,
        int sortOrder,
        long id)
        : base(tenantId)
    {
        Id = id;
        DatabaseId = databaseId;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        FieldType = string.IsNullOrWhiteSpace(fieldType) ? "string" : fieldType.Trim().ToLowerInvariant();
        Required = required;
        IsSystemField = isSystemField;
        Indexed = indexed;
        SortOrder = sortOrder;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatabaseId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string Description { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string FieldType { get; private set; }

    public bool Required { get; private set; }

    public bool IsSystemField { get; private set; }

    public bool Indexed { get; private set; }

    public int SortOrder { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Update(string name, string? description, string fieldType, bool required, bool indexed, int sortOrder)
    {
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        FieldType = string.IsNullOrWhiteSpace(fieldType) ? "string" : fieldType.Trim().ToLowerInvariant();
        Required = required;
        Indexed = indexed;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
