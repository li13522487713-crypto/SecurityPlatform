using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiWorkflowDefinition : TenantEntity
{
    public AiWorkflowDefinition()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        CanvasJson = "{}";
        DefinitionJson = "{}";
        Status = AiWorkflowStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public AiWorkflowDefinition(
        TenantId tenantId,
        string name,
        string? description,
        string canvasJson,
        string definitionJson,
        long creatorId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        CanvasJson = string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        Status = AiWorkflowStatus.Draft;
        CreatorId = creatorId;
        PublishVersion = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string CanvasJson { get; private set; }
    public string DefinitionJson { get; private set; }
    public AiWorkflowStatus Status { get; private set; }
    public int PublishVersion { get; private set; }
    public long CreatorId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public void SaveDefinition(string canvasJson, string definitionJson)
    {
        CanvasJson = string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Save(string canvasJson, string definitionJson)
    {
        SaveDefinition(canvasJson, definitionJson);
    }

    public void UpdateMeta(string name, string? description)
    {
        Name = name;
        Description = description ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = AiWorkflowStatus.Published;
        PublishVersion += 1;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        Status = AiWorkflowStatus.Disabled;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiWorkflowStatus
{
    Draft = 0,
    Published = 1,
    Disabled = 2
}
