using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

internal static class ConversionFunctions
{
    public static void Register(BuiltinFunctionRegistry r)
    {
        Reg(r, "TO_STRING", "Convert to string", new[] { P("value", ExprType.Any) }, ExprType.String,
            args => args[0]?.ToString());

        Reg(r, "TO_INT", "Convert to integer", new[] { P("value", ExprType.Any) }, ExprType.Integer,
            args => args[0] is null ? null : (object)Convert.ToInt64(args[0]));

        Reg(r, "TO_DECIMAL", "Convert to decimal", new[] { P("value", ExprType.Any) }, ExprType.Decimal,
            args => args[0] is null ? null : (object)Convert.ToDecimal(args[0]));

        Reg(r, "TO_BOOL", "Convert to boolean", new[] { P("value", ExprType.Any) }, ExprType.Boolean,
            args => args[0] is null ? null : (object)Convert.ToBoolean(args[0]));
    }

    private static FunctionParameter P(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = true };

    private static void Reg(BuiltinFunctionRegistry r, string name, string desc,
        FunctionParameter[] parameters, ExprType returnType, Func<object?[], object?> evaluator)
    {
        r.Register(new FunctionSignature
        {
            Name = name,
            Description = desc,
            Category = FunctionCategory.Conversion,
            Parameters = parameters,
            ReturnType = ExprTypeDescriptor.Of(returnType),
        }, evaluator);
    }
}
