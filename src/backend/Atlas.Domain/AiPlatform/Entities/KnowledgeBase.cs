using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class KnowledgeBase : TenantEntity
{
    public KnowledgeBase()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public KnowledgeBase(
        TenantId tenantId,
        string name,
        string? description,
        KnowledgeBaseType type,
        long id,
        long? workspaceId = null)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        WorkspaceId = workspaceId;
        Description = description ?? string.Empty;
        Type = type;
        CreatedAt = DateTime.UtcNow;
        DocumentCount = 0;
        ChunkCount = 0;
    }

    public string Name { get; private set; }
    public long? WorkspaceId { get; private set; }
    public string? Description { get; private set; }
    public KnowledgeBaseType Type { get; private set; }
    public int DocumentCount { get; private set; }
    public int ChunkCount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void Update(string name, string? description, KnowledgeBaseType type)
    {
        Name = name;
        Description = description ?? string.Empty;
        Type = type;
    }

    public void AssignWorkspace(long workspaceId)
    {
        if (workspaceId <= 0)
        {
            return;
        }

        WorkspaceId = workspaceId;
    }

    public void SetDocumentCount(int count)
    {
        DocumentCount = Math.Max(0, count);
    }

    public void SetChunkCount(int count)
    {
        ChunkCount = Math.Max(0, count);
    }
}

public enum KnowledgeBaseType
{
    Text = 0,
    Table = 1,
    Image = 2
}
