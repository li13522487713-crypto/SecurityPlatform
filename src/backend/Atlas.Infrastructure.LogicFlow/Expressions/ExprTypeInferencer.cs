using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions;

/// <summary>
/// 表达式类型推断器实现 —— 递归标注 AST 每个节点的推断类型。
/// </summary>
public sealed class ExprTypeInferencer : ITypeInferencer
{
    public ExprTypeDescriptor Infer(ExprAstNode node, TypeInferenceContext context)
    {
        var result = InferCore(node, context);
        node.InferredType = result;
        return result;
    }

    private ExprTypeDescriptor InferCore(ExprAstNode node, TypeInferenceContext context) => node switch
    {
        LiteralNode lit => InferLiteral(lit),
        IdentifierNode id => InferIdentifier(id, context),
        UnaryNode u => InferUnary(u, context),
        BinaryNode b => InferBinary(b, context),
        ConditionalNode c => InferConditional(c, context),
        MemberAccessNode m => InferMemberAccess(m, context),
        IndexAccessNode i => InferIndexAccess(i, context),
        FunctionCallNode f => InferFunctionCall(f, context),
        LambdaNode => ExprTypeDescriptor.Of(ExprType.Function),
        ListLiteralNode l => InferList(l, context),
        MapLiteralNode => ExprTypeDescriptor.Of(ExprType.Map),
        _ => ExprTypeDescriptor.Of(ExprType.Any),
    };

    private static ExprTypeDescriptor InferLiteral(LiteralNode lit) => ExprTypeDescriptor.Of(lit.LiteralType);

    private static ExprTypeDescriptor InferIdentifier(IdentifierNode id, TypeInferenceContext ctx)
        => ctx.Variables.TryGetValue(id.Name, out var t) ? t : ExprTypeDescriptor.Of(ExprType.Any);

    private ExprTypeDescriptor InferUnary(UnaryNode u, TypeInferenceContext ctx)
    {
        var operandType = Infer(u.Operand, ctx);
        return u.Operator switch
        {
            UnaryOperator.Not => ExprTypeDescriptor.Of(ExprType.Boolean),
            UnaryOperator.Negate => operandType,
            UnaryOperator.Plus => operandType,
            _ => ExprTypeDescriptor.Of(ExprType.Any),
        };
    }

    private ExprTypeDescriptor InferBinary(BinaryNode b, TypeInferenceContext ctx)
    {
        var left = Infer(b.Left, ctx);
        var right = Infer(b.Right, ctx);
        return b.Operator switch
        {
            BinaryOperator.Add when left.BaseType == ExprType.String || right.BaseType == ExprType.String
                => ExprTypeDescriptor.Of(ExprType.String),
            BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                or BinaryOperator.Divide or BinaryOperator.Modulo
                => PromoteNumeric(left, right),
            BinaryOperator.Equal or BinaryOperator.NotEqual or BinaryOperator.LessThan
                or BinaryOperator.GreaterThan or BinaryOperator.LessOrEqual
                or BinaryOperator.GreaterOrEqual or BinaryOperator.In
                => ExprTypeDescriptor.Of(ExprType.Boolean),
            BinaryOperator.And or BinaryOperator.Or
                => ExprTypeDescriptor.Of(ExprType.Boolean),
            BinaryOperator.NullCoalesce => left.BaseType == ExprType.Null ? right : left,
            _ => ExprTypeDescriptor.Of(ExprType.Any),
        };
    }

    private ExprTypeDescriptor InferConditional(ConditionalNode c, TypeInferenceContext ctx)
    {
        Infer(c.Condition, ctx);
        var trueType = Infer(c.TrueExpr, ctx);
        var falseType = Infer(c.FalseExpr, ctx);
        if (trueType.BaseType == falseType.BaseType) return trueType;
        if (trueType.IsNumeric && falseType.IsNumeric) return PromoteNumeric(trueType, falseType);
        return ExprTypeDescriptor.Of(ExprType.Any);
    }

    private ExprTypeDescriptor InferMemberAccess(MemberAccessNode m, TypeInferenceContext ctx)
    {
        Infer(m.Object, ctx);
        return ExprTypeDescriptor.Of(ExprType.Any);
    }

    private ExprTypeDescriptor InferIndexAccess(IndexAccessNode i, TypeInferenceContext ctx)
    {
        var objType = Infer(i.Object, ctx);
        Infer(i.Index, ctx);
        if (objType.BaseType == ExprType.List && objType.TypeArguments.Count > 0)
            return objType.TypeArguments[0];
        return ExprTypeDescriptor.Of(ExprType.Any);
    }

    private ExprTypeDescriptor InferFunctionCall(FunctionCallNode f, TypeInferenceContext ctx)
    {
        foreach (var arg in f.Arguments) Infer(arg, ctx);
        if (ctx.FunctionRegistry != null && ctx.FunctionRegistry.TryGet(f.FunctionName, out var fn))
            return fn!.Signature.ReturnType;
        return ExprTypeDescriptor.Of(ExprType.Any);
    }

    private ExprTypeDescriptor InferList(ListLiteralNode l, TypeInferenceContext ctx)
    {
        if (l.Elements.Count == 0) return ExprTypeDescriptor.ListOf(ExprType.Any);
        var first = Infer(l.Elements[0], ctx);
        for (int i = 1; i < l.Elements.Count; i++) Infer(l.Elements[i], ctx);
        return ExprTypeDescriptor.ListOf(first.BaseType);
    }

    private static ExprTypeDescriptor PromoteNumeric(ExprTypeDescriptor a, ExprTypeDescriptor b)
    {
        var rank = NumericRank(a.BaseType);
        var rankB = NumericRank(b.BaseType);
        var max = rank > rankB ? a.BaseType : b.BaseType;
        return ExprTypeDescriptor.Of(max);
    }

    private static int NumericRank(ExprType t) => t switch
    {
        ExprType.Integer => 1,
        ExprType.Long => 2,
        ExprType.Double => 3,
        ExprType.Decimal => 4,
        _ => 0,
    };
}
