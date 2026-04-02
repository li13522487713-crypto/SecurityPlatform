using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

/// <summary>
/// 窗口函数桩实现 —— 当前版本提供 ROW_NUMBER / RANK 语义占位，
/// 实际窗口语义需在数据集上下文中求值（Track-03 后续完善）。
/// </summary>
internal static class WindowFunctions
{
    public static void Register(BuiltinFunctionRegistry r)
    {
        Reg(r, "ROW_NUMBER", "Row number (stub - requires dataset context)", Array.Empty<FunctionParameter>(), ExprType.Integer,
            _ => 0);

        Reg(r, "RANK", "Rank (stub - requires dataset context)", Array.Empty<FunctionParameter>(), ExprType.Integer,
            _ => 0);
    }

    private static void Reg(BuiltinFunctionRegistry r, string name, string desc,
        FunctionParameter[] parameters, ExprType returnType, Func<object?[], object?> evaluator)
    {
        r.Register(new FunctionSignature
        {
            Name = name,
            Description = desc,
            Category = FunctionCategory.Window,
            Parameters = parameters,
            ReturnType = ExprTypeDescriptor.Of(returnType),
        }, evaluator);
    }
}
