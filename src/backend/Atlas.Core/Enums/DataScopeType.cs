namespace Atlas.Core.Enums;

/// <summary>
/// 数据权限范围类型（等保2.0 最小化授权原则）
/// </summary>
public enum DataScopeType
{
    /// <summary>全部数据权限</summary>
    All = 0,
    /// <summary>当前租户全部数据（兼容历史值）</summary>
    CurrentTenant = 1,
    /// <summary>自定义部门</summary>
    CustomDept = 2,
    /// <summary>本部门</summary>
    CurrentDept = 3,
    /// <summary>本部门及下级</summary>
    CurrentDeptAndBelow = 4,
    /// <summary>仅本人</summary>
    OnlySelf = 5,
    /// <summary>项目维度</summary>
    Project = 6
}
