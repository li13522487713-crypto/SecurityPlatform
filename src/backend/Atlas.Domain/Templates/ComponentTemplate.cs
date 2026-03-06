using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Templates;

/// <summary>
/// 可复用的表单/页面/流程/表格模板
/// </summary>
public sealed class ComponentTemplate : TenantEntity
{
    public ComponentTemplate()
        : base(TenantId.Empty)
    {
    }

    public ComponentTemplate(TenantId tenantId, long id)
        : base(tenantId)
    {
        SetId(id);
    }

    public string Name { get; set; } = string.Empty;
    public TemplateCategory Category { get; set; }
    public string SchemaJson { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public string Version { get; set; } = "1.0.0";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum TemplateCategory
{
    Form = 0,
    Page = 1,
    Flow = 2,
    Grid = 3
}
