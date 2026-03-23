using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 动态表关系定义（支持关联强度、基数、删除行为和汇总计算）。
/// </summary>
public sealed class DynamicRelation : TenantEntity
{
    public DynamicRelation()
        : base(TenantId.Empty)
    {
        RelatedTableKey = string.Empty;
        SourceField = string.Empty;
        TargetField = string.Empty;
        RelationType = "OneToMany";
    }

    public DynamicRelation(
        TenantId tenantId,
        long tableId,
        string relatedTableKey,
        string sourceField,
        string targetField,
        string relationType,
        string? cascadeRule,
        long id,
        DateTimeOffset now,
        RelationMultiplicity multiplicity = RelationMultiplicity.OneToMany,
        RelationOnDeleteAction onDeleteAction = RelationOnDeleteAction.NoAction,
        bool enableRollup = false,
        string? rollupDefinitionsJson = null)
        : base(tenantId)
    {
        Id = id;
        TableId = tableId;
        RelatedTableKey = relatedTableKey;
        SourceField = sourceField;
        TargetField = targetField;
        RelationType = relationType;
        CascadeRule = cascadeRule;
        CreatedAt = now;
        Multiplicity = multiplicity;
        OnDeleteAction = onDeleteAction;
        EnableRollup = enableRollup;
        RollupDefinitionsJson = rollupDefinitionsJson;
    }

    public long TableId { get; private set; }
    public string RelatedTableKey { get; private set; }
    public string SourceField { get; private set; }
    public string TargetField { get; private set; }

    /// <summary>关联强度：MasterDetail / Lookup / PolymorphicLookup</summary>
    public string RelationType { get; private set; }

    /// <summary>兼容旧字段，与 OnDeleteAction 并存。</summary>
    public string? CascadeRule { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>关联基数：1:1 / 1:N / N:N</summary>
    public RelationMultiplicity Multiplicity { get; private set; }

    /// <summary>主记录删除行为</summary>
    public RelationOnDeleteAction OnDeleteAction { get; private set; }

    /// <summary>是否启用汇总计算</summary>
    public bool EnableRollup { get; private set; }

    /// <summary>汇总计算配置 JSON（RollupDefinition 数组序列化）</summary>
    public string? RollupDefinitionsJson { get; private set; }

    public void UpdateRollup(bool enableRollup, string? rollupDefinitionsJson)
    {
        EnableRollup = enableRollup;
        RollupDefinitionsJson = rollupDefinitionsJson;
    }
}
