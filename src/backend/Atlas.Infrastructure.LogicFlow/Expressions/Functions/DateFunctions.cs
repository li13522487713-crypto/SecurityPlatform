using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

internal static class DateFunctions
{
    public static void Register(BuiltinFunctionRegistry r)
    {
        Reg(r, "NOW", "Current UTC timestamp", Array.Empty<FunctionParameter>(), ExprType.DateTime,
            _ => DateTimeOffset.UtcNow);

        Reg(r, "TODAY", "Current UTC date", Array.Empty<FunctionParameter>(), ExprType.DateTime,
            _ => DateTimeOffset.UtcNow.Date);

        Reg(r, "DATE_ADD", "Add days to date", new[] { P("date", ExprType.DateTime), P("days", ExprType.Integer) }, ExprType.DateTime,
            args => args[0] is null ? null : (object)((DateTimeOffset)args[0]!).AddDays(Convert.ToDouble(args[1])));

        Reg(r, "DATE_DIFF", "Difference in days", new[] { P("from", ExprType.DateTime), P("to", ExprType.DateTime) }, ExprType.Integer,
            args => args[0] is null || args[1] is null ? null : (object)(int)((DateTimeOffset)args[1]! - (DateTimeOffset)args[0]!).TotalDays);

        Reg(r, "YEAR", "Extract year", new[] { P("date", ExprType.DateTime) }, ExprType.Integer,
            args => args[0] is null ? null : (object)((DateTimeOffset)args[0]!).Year);

        Reg(r, "MONTH", "Extract month", new[] { P("date", ExprType.DateTime) }, ExprType.Integer,
            args => args[0] is null ? null : (object)((DateTimeOffset)args[0]!).Month);

        Reg(r, "DAY", "Extract day", new[] { P("date", ExprType.DateTime) }, ExprType.Integer,
            args => args[0] is null ? null : (object)((DateTimeOffset)args[0]!).Day);
    }

    private static FunctionParameter P(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = true };

    private static void Reg(BuiltinFunctionRegistry r, string name, string desc,
        FunctionParameter[] parameters, ExprType returnType, Func<object?[], object?> evaluator)
    {
        r.Register(new FunctionSignature
        {
            Name = name,
            Description = desc,
            Category = FunctionCategory.Date,
            Parameters = parameters,
            ReturnType = ExprTypeDescriptor.Of(returnType),
        }, evaluator);
    }
}
