namespace Atlas.Domain.LowCode.Enums;

/// <summary>
/// 低代码页面类型
/// </summary>
public enum LowCodePageType
{
    /// <summary>列表页（CRUD）</summary>
    List = 0,

    /// <summary>表单页（新增/编辑）</summary>
    Form = 1,

    /// <summary>详情页（只读）</summary>
    Detail = 2,

    /// <summary>仪表盘</summary>
    Dashboard = 3,

    /// <summary>空白页（自定义）</summary>
    Blank = 4
}
