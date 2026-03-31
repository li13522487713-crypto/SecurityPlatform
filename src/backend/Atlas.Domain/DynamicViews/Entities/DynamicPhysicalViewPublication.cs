using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicViews.Entities;

[SugarTable("DynamicPhysicalViewPublication")]
public sealed class DynamicPhysicalViewPublication : TenantEntity
{
    public DynamicPhysicalViewPublication() : base(TenantId.Empty)
    {
        ViewKey = string.Empty;
        PhysicalViewName = string.Empty;
        Status = "Published";
        DataSourceId = null;
        Comment = null;
        PublishedAt = DateTimeOffset.MinValue;
        PublishedBy = 0;
        Version = 0;
        AppId = null;
    }

    public DynamicPhysicalViewPublication(
        TenantId tenantId,
        long id,
        long? appId,
        string viewKey,
        int version,
        string physicalViewName,
        long? dataSourceId,
        string status,
        string? comment,
        long publishedBy,
        DateTimeOffset publishedAt) : base(tenantId)
    {
        Id = id;
        AppId = appId;
        ViewKey = viewKey;
        Version = version;
        PhysicalViewName = physicalViewName;
        DataSourceId = dataSourceId;
        Status = status;
        Comment = comment;
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
    }

    [SugarColumn(IsNullable = true)]
    public long? AppId { get; private set; }

    public string ViewKey { get; private set; }

    public int Version { get; private set; }

    public string PhysicalViewName { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? DataSourceId { get; private set; }

    public string Status { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? Comment { get; private set; }

    public long PublishedBy { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }
}
