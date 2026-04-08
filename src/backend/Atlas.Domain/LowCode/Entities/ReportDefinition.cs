using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Domain.LowCode.Entities;

public sealed class ReportDefinition : TenantEntity
{
    public ReportDefinition() : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        ConfigJson = string.Empty;
        DataSourceJson = string.Empty;
        PrintTemplateJson = string.Empty;
    }

    public ReportDefinition(TenantId tenantId, string name, string? description, string? category, string configJson, long createdBy, long id, DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        ConfigJson = configJson;
        DataSourceJson = string.Empty;
        PrintTemplateJson = string.Empty;
        Version = 1; Status = FormDefinitionStatus.Draft;
        CreatedAt = now; UpdatedAt = now; CreatedBy = createdBy; UpdatedBy = createdBy;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public string ConfigJson { get; private set; }
    public int Version { get; private set; }
    public FormDefinitionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }
    public string? DataSourceJson { get; private set; }
    public string? PrintTemplateJson { get; private set; }

    public void Update(string name, string? description, string? category, string configJson, long updatedBy, DateTimeOffset now)
    {
        Name = name;
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        ConfigJson = configJson;
        Version += 1; UpdatedBy = updatedBy; UpdatedAt = now;
    }

    public void UpdatePrintTemplate(string printTemplateJson, long updatedBy, DateTimeOffset now)
    {
        PrintTemplateJson = printTemplateJson; UpdatedBy = updatedBy; UpdatedAt = now;
    }

    public void SetDataSource(string dataSourceJson, long updatedBy, DateTimeOffset now)
    {
        DataSourceJson = dataSourceJson; UpdatedBy = updatedBy; UpdatedAt = now;
    }

    public void Publish(long updatedBy, DateTimeOffset now)
    {
        Status = FormDefinitionStatus.Published; UpdatedBy = updatedBy; UpdatedAt = now;
    }

    public void Disable(long updatedBy, DateTimeOffset now)
    {
        Status = FormDefinitionStatus.Disabled; UpdatedBy = updatedBy; UpdatedAt = now;
    }
}
