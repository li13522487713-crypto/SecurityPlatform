using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 组件租户级覆盖项（M06 S06-1）。
/// 用例：禁用某些组件、为某组件定制默认 props。
/// </summary>
public sealed class AppComponentOverride : TenantEntity
{
#pragma warning disable CS8618
    public AppComponentOverride()
        : base(TenantId.Empty)
    {
        Type = string.Empty;
    }
#pragma warning restore CS8618

    public AppComponentOverride(TenantId tenantId, long id, string type, bool hidden, string? defaultPropsJson)
        : base(tenantId)
    {
        Id = id;
        Type = type;
        Hidden = hidden;
        DefaultPropsJson = defaultPropsJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Type { get; private set; }

    public bool Hidden { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? DefaultPropsJson { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(bool hidden, string? defaultPropsJson)
    {
        Hidden = hidden;
        DefaultPropsJson = defaultPropsJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
