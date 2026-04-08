using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Domain.LowCode.Entities;

public sealed class DashboardDefinition : TenantEntity
{
    public DashboardDefinition() : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        LayoutJson = string.Empty;
        ThemeJson = string.Empty;
    }

    public DashboardDefinition(TenantId tenantId, string name, string? description, string? category, string layoutJson, long createdBy, long id, DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        LayoutJson = layoutJson;
        ThemeJson = string.Empty;
        Version = 1; Status = FormDefinitionStatus.Draft; IsLargeScreen = false;
        CreatedAt = now; UpdatedAt = now; CreatedBy = createdBy; UpdatedBy = createdBy;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public string LayoutJson { get; private set; }
    public int Version { get; private set; }
    public FormDefinitionStatus Status { get; private set; }
    public bool IsLargeScreen { get; private set; }
    public int? CanvasWidth { get; private set; }
    public int? CanvasHeight { get; private set; }
    public string? ThemeJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }

    public void Update(string name, string? description, string? category, string layoutJson, long updatedBy, DateTimeOffset now)
    {
        Name = name;
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        LayoutJson = layoutJson;
        Version += 1; UpdatedBy = updatedBy; UpdatedAt = now;
    }

    public void SetLargeScreenMode(bool isLargeScreen, int? width, int? height, long updatedBy, DateTimeOffset now)
    {
        IsLargeScreen = isLargeScreen; CanvasWidth = width; CanvasHeight = height;
        UpdatedBy = updatedBy; UpdatedAt = now;
    }

    public void SetTheme(string themeJson, long updatedBy, DateTimeOffset now)
    {
        ThemeJson = themeJson; UpdatedBy = updatedBy; UpdatedAt = now;
    }

    public void Publish(long updatedBy, DateTimeOffset now)
    {
        Status = FormDefinitionStatus.Published; UpdatedBy = updatedBy; UpdatedAt = now;
    }
}
