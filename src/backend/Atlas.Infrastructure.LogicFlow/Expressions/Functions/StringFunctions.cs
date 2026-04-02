using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

internal static class StringFunctions
{
    public static void Register(BuiltinFunctionRegistry r)
    {
        Reg(r, "CONCAT", "Concatenate strings", true,
            [P("values", ExprType.String)], ExprType.String,
            args => string.Concat(args.Where(a => a != null).Select(a => a!.ToString())));

        Reg(r, "UPPER", "Convert to uppercase", false,
            [P("text", ExprType.String)], ExprType.String,
            args => NullGuard(args, a => a[0]?.ToString()?.ToUpperInvariant()));

        Reg(r, "LOWER", "Convert to lowercase", false,
            [P("text", ExprType.String)], ExprType.String,
            args => NullGuard(args, a => a[0]?.ToString()?.ToLowerInvariant()));

        Reg(r, "TRIM", "Trim whitespace", false,
            [P("text", ExprType.String)], ExprType.String,
            args => NullGuard(args, a => a[0]?.ToString()?.Trim()));

        Reg(r, "LEFT", "Left substring", false,
            [P("text", ExprType.String), P("length", ExprType.Integer)], ExprType.String,
            args => NullGuard(args, a =>
            {
                var s = a[0]?.ToString() ?? "";
                var len = Convert.ToInt32(a[1]);
                return s[..Math.Min(len, s.Length)];
            }));

        Reg(r, "RIGHT", "Right substring", false,
            [P("text", ExprType.String), P("length", ExprType.Integer)], ExprType.String,
            args => NullGuard(args, a =>
            {
                var s = a[0]?.ToString() ?? "";
                var len = Convert.ToInt32(a[1]);
                return s[Math.Max(0, s.Length - len)..];
            }));

        Reg(r, "SUBSTRING", "Substring", false,
            [P("text", ExprType.String), P("start", ExprType.Integer), PO("length", ExprType.Integer)], ExprType.String,
            args => NullGuard(args, a =>
            {
                var s = a[0]?.ToString() ?? "";
                var start = Convert.ToInt32(a[1]);
                if (start < 0 || start >= s.Length) return "";
                if (a.Length > 2 && a[2] != null) return s.Substring(start, Math.Min(Convert.ToInt32(a[2]), s.Length - start));
                return s[start..];
            }));

        Reg(r, "LENGTH", "String length", false,
            [P("text", ExprType.String)], ExprType.Integer,
            args => NullGuard(args, a => a[0]?.ToString()?.Length ?? 0));

        Reg(r, "REPLACE", "Replace occurrences", false,
            [P("text", ExprType.String), P("search", ExprType.String), P("replacement", ExprType.String)], ExprType.String,
            args => NullGuard(args, a => (a[0]?.ToString() ?? "").Replace(a[1]?.ToString() ?? "", a[2]?.ToString() ?? "")));

        Reg(r, "CONTAINS", "Check if string contains substring", false,
            [P("text", ExprType.String), P("search", ExprType.String)], ExprType.Boolean,
            args => NullGuard(args, a => (a[0]?.ToString() ?? "").Contains(a[1]?.ToString() ?? "", StringComparison.Ordinal)));

        Reg(r, "STARTS_WITH", "Check if string starts with prefix", false,
            [P("text", ExprType.String), P("prefix", ExprType.String)], ExprType.Boolean,
            args => NullGuard(args, a => (a[0]?.ToString() ?? "").StartsWith(a[1]?.ToString() ?? "", StringComparison.Ordinal)));

        Reg(r, "ENDS_WITH", "Check if string ends with suffix", false,
            [P("text", ExprType.String), P("suffix", ExprType.String)], ExprType.Boolean,
            args => NullGuard(args, a => (a[0]?.ToString() ?? "").EndsWith(a[1]?.ToString() ?? "", StringComparison.Ordinal)));

        Reg(r, "SPLIT", "Split string", false,
            [P("text", ExprType.String), P("separator", ExprType.String)], ExprType.List,
            args => NullGuard(args, a => (a[0]?.ToString() ?? "").Split(a[1]?.ToString() ?? ",").ToList()));

        Reg(r, "JOIN", "Join list to string", false,
            [P("list", ExprType.List), P("separator", ExprType.String)], ExprType.String,
            args => NullGuard(args, a =>
            {
                if (a[0] is IEnumerable<object?> list)
                    return string.Join(a[1]?.ToString() ?? "", list.Select(x => x?.ToString() ?? ""));
                return "";
            }));

        Reg(r, "FORMAT", "Format string with arguments", true,
            [P("template", ExprType.String)], ExprType.String,
            args =>
            {
                if (args.Length == 0 || args[0] == null) return null;
                var template = args[0]!.ToString()!;
                var fmtArgs = args.Skip(1).Select(a => a ?? "").ToArray();
                return string.Format(template, fmtArgs);
            });
    }

    private static FunctionParameter P(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = true };
    private static FunctionParameter PO(string name, ExprType type) => new() { Name = name, Type = ExprTypeDescriptor.Of(type), IsRequired = false };

    private static object? NullGuard(object?[] args, Func<object?[], object?> fn)
    {
        if (args.Length > 0 && args[0] == null) return null;
        return fn(args);
    }

    private static void Reg(BuiltinFunctionRegistry r, string name, string desc, bool variadic,
        FunctionParameter[] parameters, ExprType returnType, Func<object?[], object?> evaluator)
    {
        r.Register(new FunctionSignature
        {
            Name = name,
            Description = desc,
            Category = FunctionCategory.String,
            Parameters = parameters,
            ReturnType = ExprTypeDescriptor.Of(returnType),
            IsVariadic = variadic,
        }, evaluator);
    }
}
