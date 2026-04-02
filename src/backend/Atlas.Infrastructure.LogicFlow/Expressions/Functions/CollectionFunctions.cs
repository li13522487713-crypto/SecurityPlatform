using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

internal static class CollectionFunctions
{
    public static void Register(BuiltinFunctionRegistry r)
    {
        Reg(r, "LIST_COUNT", "Count items in list", new[] { P("list", ExprType.List) }, ExprType.Integer,
            args => args[0] is IEnumerable<object?> list ? (object)list.Count() : 0);

        Reg(r, "LIST_CONTAINS", "Check if list contains value", new[] { P("list", ExprType.List), P("value", ExprType.Any) }, ExprType.Boolean,
            args => args[0] is IEnumerable<object?> list && list.Contains(args[1]));

        Reg(r, "LIST_FIRST", "Get first item", new[] { P("list", ExprType.List) }, ExprType.Any,
            args => args[0] is IEnumerable<object?> list ? list.FirstOrDefault() : null);

        Reg(r, "LIST_LAST", "Get last item", new[] { P("list", ExprType.List) }, ExprType.Any,
            args => args[0] is IEnumerable<object?> list ? list.LastOrDefault() : null);
    }

    private static FunctionParameter P(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = true };

    private static void Reg(BuiltinFunctionRegistry r, string name, string desc,
        FunctionParameter[] parameters, ExprType returnType, Func<object?[], object?> evaluator)
    {
        r.Register(new FunctionSignature
        {
            Name = name,
            Description = desc,
            Category = FunctionCategory.Collection,
            Parameters = parameters,
            ReturnType = ExprTypeDescriptor.Of(returnType),
        }, evaluator);
    }
}
