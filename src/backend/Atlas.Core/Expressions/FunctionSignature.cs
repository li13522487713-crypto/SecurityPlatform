namespace Atlas.Core.Expressions;

/// <summary>
/// 函数签名元数据，用于类型推断与文档生成。
/// </summary>
public sealed class FunctionSignature
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public FunctionCategory Category { get; init; }
    public IReadOnlyList<FunctionParameter> Parameters { get; init; } = [];
    public ExprTypeDescriptor ReturnType { get; init; } = ExprTypeDescriptor.Of(ExprType.Any);
    public bool IsVariadic { get; init; }
    public int MinArgs => Parameters.Count(p => p.IsRequired);
}

public sealed class FunctionParameter
{
    public required string Name { get; init; }
    public ExprTypeDescriptor Type { get; init; } = ExprTypeDescriptor.Of(ExprType.Any);
    public bool IsRequired { get; init; } = true;
    public object? DefaultValue { get; init; }
}

public enum FunctionCategory
{
    String = 1,
    Numeric = 2,
    Date = 3,
    Conversion = 4,
    Collection = 5,
    Aggregate = 6,
    Window = 7,
    Logic = 8,
    Custom = 99,
}
