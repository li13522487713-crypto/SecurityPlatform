using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicViews.Entities;

[SugarTable("DynamicViewVersion")]
public sealed class DynamicViewVersion : TenantEntity
{
    public DynamicViewVersion() : base(TenantId.Empty)
    {
        ViewKey = string.Empty;
        DefinitionJson = "{}";
        Checksum = string.Empty;
        Comment = null;
        Status = "Published";
        CreatedAt = DateTimeOffset.MinValue;
        CreatedBy = 0;
    }

    public DynamicViewVersion(
        TenantId tenantId,
        long id,
        long? appId,
        string viewKey,
        int version,
        string definitionJson,
        string checksum,
        string status,
        string? comment,
        long createdBy,
        DateTimeOffset createdAt) : base(tenantId)
    {
        Id = id;
        AppId = appId;
        ViewKey = viewKey;
        Version = version;
        DefinitionJson = definitionJson;
        Checksum = checksum;
        Status = status;
        Comment = comment;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    [SugarColumn(IsNullable = true)]
    public long? AppId { get; private set; }

    public string ViewKey { get; private set; }

    public int Version { get; private set; }

    public string DefinitionJson { get; private set; }

    public string Checksum { get; private set; }

    public string Status { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? Comment { get; private set; }

    public long CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
