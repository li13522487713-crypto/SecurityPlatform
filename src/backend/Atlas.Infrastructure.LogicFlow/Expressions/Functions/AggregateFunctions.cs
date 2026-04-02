using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

internal static class AggregateFunctions
{
    public static void Register(BuiltinFunctionRegistry r)
    {
        Reg(r, "SUM", "Sum of list values", new[] { P("list", ExprType.List) }, ExprType.Decimal,
            args => args[0] is IEnumerable<object?> list
                ? (object)list.Where(x => x is not null).Sum(x => Convert.ToDecimal(x))
                : 0m);

        Reg(r, "AVG", "Average of list values", new[] { P("list", ExprType.List) }, ExprType.Decimal,
            args =>
            {
                if (args[0] is not IEnumerable<object?> list) return 0m;
                var nums = list.Where(x => x is not null).Select(x => Convert.ToDecimal(x)).ToList();
                return nums.Count == 0 ? 0m : (object)nums.Average();
            });

        Reg(r, "COUNT", "Count of list items", new[] { P("list", ExprType.List) }, ExprType.Integer,
            args => args[0] is IEnumerable<object?> list ? (object)list.Count() : 0);

        Reg(r, "LIST_MIN", "Minimum value in list", new[] { P("list", ExprType.List) }, ExprType.Decimal,
            args =>
            {
                if (args[0] is not IEnumerable<object?> list) return null;
                var nums = list.Where(x => x is not null).Select(x => Convert.ToDecimal(x)).ToList();
                return nums.Count == 0 ? null : (object)nums.Min();
            });

        Reg(r, "LIST_MAX", "Maximum value in list", new[] { P("list", ExprType.List) }, ExprType.Decimal,
            args =>
            {
                if (args[0] is not IEnumerable<object?> list) return null;
                var nums = list.Where(x => x is not null).Select(x => Convert.ToDecimal(x)).ToList();
                return nums.Count == 0 ? null : (object)nums.Max();
            });
    }

    private static FunctionParameter P(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = true };

    private static void Reg(BuiltinFunctionRegistry r, string name, string desc,
        FunctionParameter[] parameters, ExprType returnType, Func<object?[], object?> evaluator)
    {
        r.Register(new FunctionSignature
        {
            Name = name,
            Description = desc,
            Category = FunctionCategory.Aggregate,
            Parameters = parameters,
            ReturnType = ExprTypeDescriptor.Of(returnType),
        }, evaluator);
    }
}
