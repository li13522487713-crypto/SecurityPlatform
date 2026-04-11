using Atlas.Core.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions.Parsing;

namespace Atlas.Infrastructure.LogicFlow.Expressions;

/// <summary>
/// AST 求值器 —— 对解析后的 AST 递归求值。
/// 支持 null 传播（T03-15）和 lambda 集合函数（T03-12）。
/// </summary>
public sealed class ExprEvaluator
{
    private readonly IFunctionRegistry _functions;
    private readonly IAstCache _cache;

    public ExprEvaluator(IFunctionRegistry functions, IAstCache cache)
    {
        _functions = functions;
        _cache = cache;
    }

    public ExprAstNode ParseAndCache(string expression)
    {
        if (_cache.TryGet(expression, out var cached)) return cached!;
        var tokens = new ExprLexer(expression).Tokenize();
        var ast = new ExprParser(tokens).Parse();
        _cache.Set(expression, ast);
        return ast;
    }

    public object? Evaluate(ExprAstNode node, ExpressionContext context)
    {
        try
        {
            return EvalNode(node, context, new Dictionary<string, object?>());
        }
        catch (InvalidOperationException ex)
        {
            throw new ExpressionEvaluationException(
                $"表达式求值失败：{ex.Message}。请检查运算符、函数名和操作数类型是否正确。",
                ex);
        }
        catch (Exception ex) when (ex is not ExpressionEvaluationException)
        {
            throw new ExpressionEvaluationException(
                $"表达式求值时发生意外错误：{ex.Message}。",
                ex);
        }
    }

    /// <summary>
    /// RT-14: 带表达式文本上下文的入口，错误信息包含原始表达式。
    /// </summary>
    public object? EvaluateWithContext(string expression, ExpressionContext context)
    {
        try
        {
            var ast = ParseAndCache(expression);
            return EvalNode(ast, context, new Dictionary<string, object?>());
        }
        catch (ExpressionEvaluationException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw new ExpressionEvaluationException(
                $"表达式 `{TruncateExpression(expression)}` 求值失败：{ex.Message}。请检查变量名是否存在、类型是否兼容。",
                ex);
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException(
                $"表达式 `{TruncateExpression(expression)}` 执行时发生错误：{ex.Message}。",
                ex);
        }
    }

    private static string TruncateExpression(string expr) =>
        expr.Length > 80 ? string.Concat(expr.AsSpan(0, 77), "...") : expr;

    private object? EvalNode(ExprAstNode node, ExpressionContext ctx, Dictionary<string, object?> locals) => node switch
    {
        LiteralNode lit => lit.Value,
        IdentifierNode id => ResolveIdentifier(id.Name, ctx, locals),
        UnaryNode u => EvalUnary(u, ctx, locals),
        BinaryNode b => EvalBinary(b, ctx, locals),
        ConditionalNode c => EvalConditional(c, ctx, locals),
        MemberAccessNode m => EvalMemberAccess(m, ctx, locals),
        IndexAccessNode i => EvalIndexAccess(i, ctx, locals),
        FunctionCallNode f => EvalFunctionCall(f, ctx, locals),
        ListLiteralNode l => l.Elements.Select(e => EvalNode(e, ctx, locals)).ToList(),
        MapLiteralNode mp => mp.Entries.ToDictionary(
            e => EvalNode(e.Key, ctx, locals)?.ToString() ?? "",
            e => EvalNode(e.Value, ctx, locals)),
        LambdaNode => node,
        _ => throw new InvalidOperationException($"Unknown node type: {node.GetType().Name}"),
    };

    private static object? ResolveIdentifier(string name, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        if (locals.TryGetValue(name, out var lv)) return lv;
        ctx.TryGetVariable(name, out var v);
        return v;
    }

    private object? EvalUnary(UnaryNode u, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        var val = EvalNode(u.Operand, ctx, locals);
        return u.Operator switch
        {
            UnaryOperator.Not => val == null ? null : !ToBool(val),
            UnaryOperator.Negate => val == null ? null : NegateNumeric(val),
            UnaryOperator.Plus => val,
            _ => throw new InvalidOperationException($"不支持的一元运算符: {u.Operator}"),
        };
    }

    private object? EvalBinary(BinaryNode b, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        // Short-circuit for && and ||
        if (b.Operator == BinaryOperator.And)
        {
            var left = EvalNode(b.Left, ctx, locals);
            if (left != null && !ToBool(left)) return false;
            var right = EvalNode(b.Right, ctx, locals);
            if (left == null || right == null) return null;
            return ToBool(right);
        }
        if (b.Operator == BinaryOperator.Or)
        {
            var left = EvalNode(b.Left, ctx, locals);
            if (left != null && ToBool(left)) return true;
            var right = EvalNode(b.Right, ctx, locals);
            if (left == null || right == null) return null;
            return ToBool(right);
        }
        if (b.Operator == BinaryOperator.NullCoalesce)
        {
            var left = EvalNode(b.Left, ctx, locals);
            return left ?? EvalNode(b.Right, ctx, locals);
        }

        var lVal = EvalNode(b.Left, ctx, locals);
        var rVal = EvalNode(b.Right, ctx, locals);

        // null 传播：算术与比较中 null 参与则结果为 null（等值比较例外）
        if (b.Operator is not (BinaryOperator.Equal or BinaryOperator.NotEqual or BinaryOperator.In))
        {
            if (lVal == null || rVal == null) return null;
        }

        return b.Operator switch
        {
            BinaryOperator.Add => Add(lVal, rVal),
            BinaryOperator.Subtract => ArithOp(lVal!, rVal!, (a, bb) => a - bb),
            BinaryOperator.Multiply => ArithOp(lVal!, rVal!, (a, bb) => a * bb),
            BinaryOperator.Divide => ArithOp(lVal!, rVal!, (a, bb) => bb == 0 ? double.NaN : a / bb),
            BinaryOperator.Modulo => ArithOp(lVal!, rVal!, (a, bb) => bb == 0 ? double.NaN : a % bb),
            BinaryOperator.Equal => Equals(lVal, rVal),
            BinaryOperator.NotEqual => !Equals(lVal, rVal),
            BinaryOperator.LessThan => Compare(lVal!, rVal!) < 0,
            BinaryOperator.GreaterThan => Compare(lVal!, rVal!) > 0,
            BinaryOperator.LessOrEqual => Compare(lVal!, rVal!) <= 0,
            BinaryOperator.GreaterOrEqual => Compare(lVal!, rVal!) >= 0,
            BinaryOperator.In => EvalIn(lVal, rVal),
            _ => throw new InvalidOperationException($"Unknown operator: {b.Operator}"),
        };
    }

    private object? EvalConditional(ConditionalNode c, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        var cond = EvalNode(c.Condition, ctx, locals);
        return ToBool(cond) ? EvalNode(c.TrueExpr, ctx, locals) : EvalNode(c.FalseExpr, ctx, locals);
    }

    private object? EvalMemberAccess(MemberAccessNode m, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        var obj = EvalNode(m.Object, ctx, locals);
        if (obj == null) return null;
        if (obj is IDictionary<string, object?> dict)
            return dict.TryGetValue(m.MemberName, out var v) ? v : null;
        if (obj is IReadOnlyDictionary<string, object?> rdict)
            return rdict.TryGetValue(m.MemberName, out var rv) ? rv : null;
        var prop = obj.GetType().GetProperty(m.MemberName);
        return prop?.GetValue(obj);
    }

    private object? EvalIndexAccess(IndexAccessNode i, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        var obj = EvalNode(i.Object, ctx, locals);
        var index = EvalNode(i.Index, ctx, locals);
        if (obj == null) return null;
        if (obj is IList<object?> list && index != null)
        {
            var idx = Convert.ToInt32(index);
            return idx >= 0 && idx < list.Count ? list[idx] : null;
        }
        if (obj is IDictionary<string, object?> dict && index != null)
            return dict.TryGetValue(index.ToString()!, out var v) ? v : null;
        return null;
    }

    private object? EvalFunctionCall(FunctionCallNode f, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        // Lambda 集合函数特殊处理
        if (IsLambdaCollectionFunction(f.FunctionName) && f.Arguments.Count >= 2 && f.Arguments[1] is LambdaNode lambda)
            return EvalLambdaCollectionFunction(f.FunctionName, f.Arguments, lambda, ctx, locals);

        var args = f.Arguments.Select(a => EvalNode(a, ctx, locals)).ToArray();

        if (!_functions.TryGet(f.FunctionName, out var fn))
            throw new InvalidOperationException($"Unknown function: {f.FunctionName}");

        return fn!.Evaluator(args);
    }

    private object? EvalLambdaCollectionFunction(string name, IReadOnlyList<ExprAstNode> arguments,
        LambdaNode lambda, ExpressionContext ctx, Dictionary<string, object?> locals)
    {
        var listVal = EvalNode(arguments[0], ctx, locals);
        var list = listVal switch
        {
            List<object?> l => l,
            IEnumerable<object?> e => e.ToList(),
            _ => new List<object?>(),
        };

        var paramName = lambda.Parameters.Count > 0 ? lambda.Parameters[0] : "_";

        object? EvalLambdaItem(object? item)
        {
            var newLocals = new Dictionary<string, object?>(locals) { [paramName] = item };
            return EvalNode(lambda.Body, ctx, newLocals);
        }

        return name.ToUpperInvariant() switch
        {
            "MAP" => list.Select(EvalLambdaItem).ToList(),
            "FILTER" => list.Where(item => ToBool(EvalLambdaItem(item))).ToList(),
            "FIND" => list.FirstOrDefault(item => ToBool(EvalLambdaItem(item))),
            "SOME" => list.Any(item => ToBool(EvalLambdaItem(item))),
            "EVERY" => list.All(item => ToBool(EvalLambdaItem(item))),
            "FLAT_MAP" => list.SelectMany(item =>
            {
                var result = EvalLambdaItem(item);
                return result is IEnumerable<object?> subList ? subList : [result];
            }).ToList(),
            "REDUCE" =>
                list.Aggregate(
                    arguments.Count > 2 ? EvalNode(arguments[2], ctx, locals) : null,
                    (acc, item) =>
                    {
                        var accParam = lambda.Parameters.Count > 1 ? lambda.Parameters[1] : "__acc";
                        var newLocals = new Dictionary<string, object?>(locals) { [paramName] = item, [accParam] = acc };
                        return EvalNode(lambda.Body, ctx, newLocals);
                    }),
            _ => null,
        };
    }

    private static bool IsLambdaCollectionFunction(string name) =>
        name.Equals("MAP", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("FILTER", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("FIND", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SOME", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("EVERY", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("FLAT_MAP", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("REDUCE", StringComparison.OrdinalIgnoreCase);

    private static object? Add(object? left, object? right)
    {
        if (left is string || right is string)
            return (left?.ToString() ?? "") + (right?.ToString() ?? "");
        if (left == null || right == null) return null;
        return ArithOp(left, right, (a, b) => a + b);
    }

    private static object? ArithOp(object left, object right, Func<double, double, double> op)
    {
        var a = Convert.ToDouble(left);
        var b = Convert.ToDouble(right);
        var result = op(a, b);
        if (left is int && right is int && result is >= int.MinValue and <= int.MaxValue && result == Math.Floor(result))
            return (int)result;
        return result;
    }

    private static new bool Equals(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;
        if (left is IConvertible && right is IConvertible)
        {
            try { return Convert.ToDouble(left) == Convert.ToDouble(right); }
            catch { /* fall through */ }
        }
        return left.Equals(right);
    }

    private static int Compare(object left, object right)
    {
        if (left is IComparable cl) return cl.CompareTo(right);
        return Comparer<object>.Default.Compare(left, right);
    }

    private static bool ToBool(object? val) => val switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        double d => d != 0,
        string s => s.Length > 0,
        _ => true,
    };

    private static object NegateNumeric(object val) => val switch
    {
        int i => -i,
        long l => -l,
        double d => -d,
        decimal m => -m,
        _ => -Convert.ToDouble(val),
    };

    private static bool EvalIn(object? left, object? right)
    {
        if (right is IEnumerable<object?> list) return list.Any(item => Equals(left, item));
        if (right is string s && left is string sub) return s.Contains(sub, StringComparison.Ordinal);
        return false;
    }
}
