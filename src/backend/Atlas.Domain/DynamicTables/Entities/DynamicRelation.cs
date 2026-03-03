using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 动态表轻量关系定义（用于生成器消费，不直接创建 DB 外键）。
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
        DateTimeOffset now)
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
    }

    public long TableId { get; private set; }
    public string RelatedTableKey { get; private set; }
    public string SourceField { get; private set; }
    public string TargetField { get; private set; }
    public string RelationType { get; private set; }
    public string? CascadeRule { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
