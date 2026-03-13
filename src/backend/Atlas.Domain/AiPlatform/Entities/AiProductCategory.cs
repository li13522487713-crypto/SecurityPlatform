using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiProductCategory : TenantEntity
{
    public AiProductCategory()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        Description = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiProductCategory(
        TenantId tenantId,
        string name,
        string code,
        string? description,
        int sortOrder,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Code = code;
        Description = description ?? string.Empty;
        SortOrder = sortOrder;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(string name, string code, string? description, int sortOrder)
    {
        Name = name;
        Code = code;
        Description = description ?? string.Empty;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
