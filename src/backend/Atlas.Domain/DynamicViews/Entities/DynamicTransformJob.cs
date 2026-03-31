using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicViews.Entities;

[SugarTable("DynamicTransformJob")]
public sealed class DynamicTransformJob : TenantEntity
{
    public DynamicTransformJob() : base(TenantId.Empty)
    {
        JobKey = string.Empty;
        Name = string.Empty;
        DefinitionJson = "{}";
        Status = "Draft";
        AppId = null;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
        CreatedBy = 0;
        UpdatedBy = 0;
    }

    public long? AppId { get; private set; }

    public string JobKey { get; private set; }

    public string Name { get; private set; }

    public string DefinitionJson { get; private set; }

    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long CreatedBy { get; private set; }

    public long UpdatedBy { get; private set; }
}
