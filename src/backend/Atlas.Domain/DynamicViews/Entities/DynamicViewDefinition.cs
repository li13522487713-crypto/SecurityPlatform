using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicViews.Entities;

[SugarTable("DynamicViewDefinition")]
public sealed class DynamicViewDefinition : TenantEntity
{
    public DynamicViewDefinition() : base(TenantId.Empty)
    {
        ViewKey = string.Empty;
        Name = string.Empty;
        Description = null;
        DefinitionJson = "{}";
        DraftDefinitionJson = "{}";
        IsPublished = false;
        PublishedVersion = 0;
        PublishedAt = null;
        PublishedBy = null;
        AppId = null;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
        CreatedBy = 0;
        UpdatedBy = 0;
    }

    public DynamicViewDefinition(
        TenantId tenantId,
        long id,
        long? appId,
        string viewKey,
        string name,
        string? description,
        string definitionJson,
        long createdBy,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        AppId = appId;
        ViewKey = viewKey;
        Name = name;
        Description = description;
        DefinitionJson = definitionJson;
        DraftDefinitionJson = definitionJson;
        IsPublished = false;
        PublishedVersion = 0;
        PublishedAt = null;
        PublishedBy = null;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    [SugarColumn(IsNullable = true)]
    public long? AppId { get; private set; }

    public string ViewKey { get; private set; }

    public string Name { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? Description { get; private set; }

    public string DefinitionJson { get; private set; }

    public string DraftDefinitionJson { get; private set; }

    public bool IsPublished { get; private set; }

    public int PublishedVersion { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? PublishedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? PublishedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long CreatedBy { get; private set; }

    public long UpdatedBy { get; private set; }

    public void UpdateDraft(string name, string? description, string draftJson, long updatedBy, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        DraftDefinitionJson = draftJson;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Publish(string definitionJson, int version, long updatedBy, DateTimeOffset now)
    {
        DefinitionJson = definitionJson;
        DraftDefinitionJson = definitionJson;
        IsPublished = true;
        PublishedVersion = version;
        PublishedAt = now;
        PublishedBy = updatedBy;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Rollback(string definitionJson, int version, long updatedBy, DateTimeOffset now)
    {
        Publish(definitionJson, version, updatedBy, now);
    }
}
