using System.Text.Json.Serialization;

namespace Atlas.Core.Expressions;

/// <summary>
/// 表达式 AST 节点基类。
/// 使用 System.Text.Json 多态序列化实现 JSON 往返一致（T03-05）。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LiteralNode), "literal")]
[JsonDerivedType(typeof(IdentifierNode), "identifier")]
[JsonDerivedType(typeof(UnaryNode), "unary")]
[JsonDerivedType(typeof(BinaryNode), "binary")]
[JsonDerivedType(typeof(ConditionalNode), "conditional")]
[JsonDerivedType(typeof(MemberAccessNode), "memberAccess")]
[JsonDerivedType(typeof(IndexAccessNode), "indexAccess")]
[JsonDerivedType(typeof(FunctionCallNode), "functionCall")]
[JsonDerivedType(typeof(LambdaNode), "lambda")]
[JsonDerivedType(typeof(ListLiteralNode), "listLiteral")]
[JsonDerivedType(typeof(MapLiteralNode), "mapLiteral")]
public abstract class ExprAstNode
{
    public int StartPos { get; init; }
    public int EndPos { get; init; }

    /// <summary>推断出的结果类型（由 ITypeInferencer 填充）。</summary>
    [JsonIgnore]
    public ExprTypeDescriptor? InferredType { get; set; }
}

public sealed class LiteralNode : ExprAstNode
{
    public ExprType LiteralType { get; init; }
    public object? Value { get; init; }
}

public sealed class IdentifierNode : ExprAstNode
{
    public required string Name { get; init; }
}

public enum UnaryOperator { Not, Negate, Plus }

public sealed class UnaryNode : ExprAstNode
{
    public UnaryOperator Operator { get; init; }
    public required ExprAstNode Operand { get; init; }
}

public enum BinaryOperator
{
    Add, Subtract, Multiply, Divide, Modulo,
    Equal, NotEqual, LessThan, GreaterThan, LessOrEqual, GreaterOrEqual,
    And, Or,
    NullCoalesce,
    In,
}

public sealed class BinaryNode : ExprAstNode
{
    public BinaryOperator Operator { get; init; }
    public required ExprAstNode Left { get; init; }
    public required ExprAstNode Right { get; init; }
}

public sealed class ConditionalNode : ExprAstNode
{
    public required ExprAstNode Condition { get; init; }
    public required ExprAstNode TrueExpr { get; init; }
    public required ExprAstNode FalseExpr { get; init; }
}

public sealed class MemberAccessNode : ExprAstNode
{
    public required ExprAstNode Object { get; init; }
    public required string MemberName { get; init; }
}

public sealed class IndexAccessNode : ExprAstNode
{
    public required ExprAstNode Object { get; init; }
    public required ExprAstNode Index { get; init; }
}

public sealed class FunctionCallNode : ExprAstNode
{
    public required string FunctionName { get; init; }
    public IReadOnlyList<ExprAstNode> Arguments { get; init; } = [];
}

public sealed class LambdaNode : ExprAstNode
{
    public IReadOnlyList<string> Parameters { get; init; } = [];
    public required ExprAstNode Body { get; init; }
}

public sealed class ListLiteralNode : ExprAstNode
{
    public IReadOnlyList<ExprAstNode> Elements { get; init; } = [];
}

public sealed class MapLiteralNode : ExprAstNode
{
    public IReadOnlyList<MapEntry> Entries { get; init; } = [];
}

public sealed class MapEntry
{
    public required ExprAstNode Key { get; init; }
    public required ExprAstNode Value { get; init; }
}
