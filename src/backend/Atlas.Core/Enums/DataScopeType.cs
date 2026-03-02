namespace Atlas.Core.Enums;

/// <summary>
/// 数据权限范围类型（等保2.0 最小化授权原则）
/// </summary>
public enum DataScopeType
{
    /// <summary>全部数据（超级管理员）</summary>
    All = 0,

    /// <summary>当前租户全部数据（默认，已由多租户隔离保证）</summary>
    CurrentTenant = 1,

    /// <summary>仅本人创建/拥有的数据</summary>
    OnlySelf = 2,

    /// <summary>自定义部门（预留）</summary>
    CustomDept = 10,

    /// <summary>本部门数据（预留）</summary>
    CurrentDept = 11,

    /// <summary>本部门及下级（预留）</summary>
    CurrentDeptAndBelow = 12
}
