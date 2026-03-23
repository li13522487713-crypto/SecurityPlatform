namespace Atlas.Domain.DynamicTables.Enums;

/// <summary>
/// 动态关系的基数类型（1:1 / 1:N / N:N）
/// </summary>
public enum RelationMultiplicity
{
    /// <summary>一对一关联</summary>
    OneToOne = 0,

    /// <summary>一对多关联</summary>
    OneToMany = 1,

    /// <summary>多对多关联（自动创建中间关联表）</summary>
    ManyToMany = 2
}
