namespace Atlas.Domain.DynamicTables.Enums;

/// <summary>
/// 主记录删除时，对子记录执行的级联行为
/// </summary>
public enum RelationOnDeleteAction
{
    /// <summary>不执行任何操作</summary>
    NoAction = 0,

    /// <summary>级联删除所有关联子记录</summary>
    Cascade = 1,

    /// <summary>将子记录的外键字段置空</summary>
    SetNull = 2,

    /// <summary>若存在关联子记录则阻止主记录删除</summary>
    Restrict = 3
}
