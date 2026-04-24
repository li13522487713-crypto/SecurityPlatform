using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiPromptTemplate : TenantEntity
{
    public AiPromptTemplate()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        Content = string.Empty;
        TagsJson = "[]";
        ResourceSource = LibrarySource.Custom;
        CreatedAt = DateTime.UtcNow;
    }

    public AiPromptTemplate(
        TenantId tenantId,
        string name,
        string? description,
        string? category,
        string content,
        string? tagsJson,
        bool isSystem,
        long id,
        LibrarySource resourceSource = LibrarySource.Custom)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        Content = content;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        IsSystem = isSystem;
        ResourceSource = resourceSource;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public string Content { get; private set; }
    public string TagsJson { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    [SugarColumn(IsNullable = false)]
    public LibrarySource ResourceSource { get; private set; }

    public void Update(
        string name,
        string? description,
        string? category,
        string content,
        string? tagsJson)
    {
        Name = name;
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        Content = content;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
