namespace Atlas.Core.Expressions;

/// <summary>
/// 表达式求值上下文，支持多层变量分层：Global / Tenant / App / Page / User / Record
/// </summary>
public sealed class ExpressionContext
{
    /// <summary>全局变量（系统级别）</summary>
    public IReadOnlyDictionary<string, object?> Global { get; init; } = new Dictionary<string, object?>();

    /// <summary>租户级变量</summary>
    public IReadOnlyDictionary<string, object?> Tenant { get; init; } = new Dictionary<string, object?>();

    /// <summary>应用级变量</summary>
    public IReadOnlyDictionary<string, object?> App { get; init; } = new Dictionary<string, object?>();

    /// <summary>页面级变量</summary>
    public IReadOnlyDictionary<string, object?> Page { get; init; } = new Dictionary<string, object?>();

    /// <summary>当前用户信息</summary>
    public IReadOnlyDictionary<string, object?> User { get; init; } = new Dictionary<string, object?>();

    /// <summary>当前记录字段值（表单字段 / 动态表行数据）</summary>
    public IReadOnlyDictionary<string, object?> Record { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 从扁平字典中简单构造 ExpressionContext（所有变量放入 Record 层）
    /// </summary>
    public static ExpressionContext FromRecord(IReadOnlyDictionary<string, object?> record)
        => new() { Record = record };

    /// <summary>
    /// 按分层顺序（Record → Page → App → Tenant → Global）查找变量值。
    /// </summary>
    public bool TryGetVariable(string name, out object? value)
    {
        if (Record.TryGetValue(name, out value)) return true;
        if (Page.TryGetValue(name, out value)) return true;
        if (App.TryGetValue(name, out value)) return true;
        if (User.TryGetValue(name, out value)) return true;
        if (Tenant.TryGetValue(name, out value)) return true;
        if (Global.TryGetValue(name, out value)) return true;
        value = null;
        return false;
    }
}
