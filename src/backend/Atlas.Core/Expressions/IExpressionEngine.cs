namespace Atlas.Core.Expressions;

/// <summary>
/// 通用表达式引擎抽象，支持 CEL 子集语法。
/// 实现须保证：无副作用、无网络访问、无反射、无动态代码执行。
/// </summary>
public interface IExpressionEngine
{
    /// <summary>
    /// 静态校验表达式语法（不求值）。
    /// </summary>
    ExpressionValidationResult Validate(string expression);

    /// <summary>
    /// 带上下文求值，返回 bool 结果（用于条件分支场景）。
    /// </summary>
    bool EvaluateBool(string expression, ExpressionContext context);

    /// <summary>
    /// 带上下文求值，返回任意类型的值（用于字段默认值 / 可见性等场景）。
    /// 返回 null 表示求值失败或结果为空。
    /// </summary>
    object? Evaluate(string expression, ExpressionContext context);

    /// <summary>
    /// 提取表达式中引用的变量名列表（用于前端类型提示）。
    /// </summary>
    IReadOnlyList<string> GetVariables(string expression);
}
