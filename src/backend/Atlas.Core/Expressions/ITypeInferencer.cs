namespace Atlas.Core.Expressions;

/// <summary>
/// 表达式类型推断器 —— 对 AST 执行静态类型分析。
/// </summary>
public interface ITypeInferencer
{
    /// <summary>
    /// 推断 AST 节点的返回类型（递归标注每个节点的 InferredType）。
    /// </summary>
    ExprTypeDescriptor Infer(ExprAstNode node, TypeInferenceContext context);
}

/// <summary>
/// 类型推断上下文，携带变量类型绑定和函数签名。
/// </summary>
public sealed class TypeInferenceContext
{
    public IReadOnlyDictionary<string, ExprTypeDescriptor> Variables { get; init; }
        = new Dictionary<string, ExprTypeDescriptor>();

    public IFunctionRegistry? FunctionRegistry { get; init; }
}
