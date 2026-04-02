using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

internal static class NumericFunctions
{
    public static void Register(BuiltinFunctionRegistry r)
    {
        Reg(r, "ABS", "Absolute value", new[] { P("value", ExprType.Decimal) }, ExprType.Decimal,
            args => args[0] is null ? null : Math.Abs(Convert.ToDecimal(args[0])));

        Reg(r, "ROUND", "Round number", new[] { P("value", ExprType.Decimal), PO("digits", ExprType.Integer) }, ExprType.Decimal,
            args => args[0] is null ? null : Math.Round(Convert.ToDecimal(args[0]),
                args.Length > 1 && args[1] is not null ? Convert.ToInt32(args[1]) : 0));

        Reg(r, "FLOOR", "Floor", new[] { P("value", ExprType.Decimal) }, ExprType.Integer,
            args => args[0] is null ? null : (object)(int)Math.Floor(Convert.ToDouble(args[0])));

        Reg(r, "CEIL", "Ceiling", new[] { P("value", ExprType.Decimal) }, ExprType.Integer,
            args => args[0] is null ? null : (object)(int)Math.Ceiling(Convert.ToDouble(args[0])));

        Reg(r, "MOD", "Modulo", new[] { P("a", ExprType.Integer), P("b", ExprType.Integer) }, ExprType.Integer,
            args => args[0] is null || args[1] is null ? null : (object)(Convert.ToInt64(args[0]) % Convert.ToInt64(args[1])));

        Reg(r, "MIN_NUM", "Minimum of two numbers", new[] { P("a", ExprType.Decimal), P("b", ExprType.Decimal) }, ExprType.Decimal,
            args => args[0] is null || args[1] is null ? null : (object)Math.Min(Convert.ToDecimal(args[0]), Convert.ToDecimal(args[1])));

        Reg(r, "MAX_NUM", "Maximum of two numbers", new[] { P("a", ExprType.Decimal), P("b", ExprType.Decimal) }, ExprType.Decimal,
            args => args[0] is null || args[1] is null ? null : (object)Math.Max(Convert.ToDecimal(args[0]), Convert.ToDecimal(args[1])));
    }

    private static FunctionParameter P(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = true };
    private static FunctionParameter PO(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = false };

    private static void Reg(BuiltinFunctionRegistry r, string name, string desc,
        FunctionParameter[] parameters, ExprType returnType, Func<object?[], object?> evaluator)
    {
        r.Register(new FunctionSignature
        {
            Name = name,
            Description = desc,
            Category = FunctionCategory.Numeric,
            Parameters = parameters,
            ReturnType = ExprTypeDescriptor.Of(returnType),
        }, evaluator);
    }
}
