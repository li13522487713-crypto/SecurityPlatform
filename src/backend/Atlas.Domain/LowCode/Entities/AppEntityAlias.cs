using Atlas.Core.Abstractions;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 应用实体别名配置（用于用户/角色/部门等实体在应用内的显示名称定制）。
/// </summary>
public sealed class AppEntityAlias : EntityBase
{
    public AppEntityAlias()
    {
        EntityType = string.Empty;
        SingularAlias = string.Empty;
    }

    public AppEntityAlias(
        long appId,
        string entityType,
        string singularAlias,
        string? pluralAlias,
        long id)
    {
        Id = id;
        AppId = appId;
        EntityType = entityType;
        SingularAlias = singularAlias;
        PluralAlias = pluralAlias;
    }

    public long AppId { get; private set; }

    /// <summary>实体类型：user / role / department</summary>
    public string EntityType { get; private set; }

    /// <summary>单数别名</summary>
    public string SingularAlias { get; private set; }

    /// <summary>复数别名</summary>
    public string? PluralAlias { get; private set; }

    public void Update(string singularAlias, string? pluralAlias)
    {
        SingularAlias = singularAlias;
        PluralAlias = pluralAlias;
    }
}
