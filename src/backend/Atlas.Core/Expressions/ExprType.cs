namespace Atlas.Core.Expressions;

/// <summary>
/// 表达式类型系统 —— 覆盖基础类型与业务类型。
/// 类型推断、函数签名校验均依赖此枚举。
/// </summary>
public enum ExprType
{
    Null = 0,
    Boolean = 1,
    Integer = 2,
    Long = 3,
    Double = 4,
    Decimal = 5,
    String = 6,
    DateTime = 7,
    Duration = 8,
    List = 20,
    Map = 21,
    Record = 22,
    Function = 30,
    Any = 98,
    Void = 99,
    Error = 100,
}

/// <summary>
/// 带泛型参数的类型描述（如 List&lt;String&gt;、Map&lt;String,Integer&gt;）。
/// </summary>
public sealed class ExprTypeDescriptor
{
    public ExprType BaseType { get; init; }
    public IReadOnlyList<ExprTypeDescriptor> TypeArguments { get; init; } = [];

    public static ExprTypeDescriptor Of(ExprType type) => new() { BaseType = type };

    public static ExprTypeDescriptor ListOf(ExprType elementType)
        => new() { BaseType = ExprType.List, TypeArguments = [Of(elementType)] };

    public static ExprTypeDescriptor MapOf(ExprType keyType, ExprType valueType)
        => new() { BaseType = ExprType.Map, TypeArguments = [Of(keyType), Of(valueType)] };

    public bool IsNumeric => BaseType is ExprType.Integer or ExprType.Long or ExprType.Double or ExprType.Decimal;

    public bool IsAssignableTo(ExprTypeDescriptor target)
    {
        if (target.BaseType == ExprType.Any) return true;
        if (BaseType == ExprType.Null) return true;
        if (BaseType == target.BaseType && TypeArguments.Count == 0 && target.TypeArguments.Count == 0) return true;
        if (IsNumeric && target.IsNumeric) return true;
        if (BaseType == target.BaseType && TypeArguments.Count == target.TypeArguments.Count)
            return TypeArguments.Zip(target.TypeArguments).All(p => p.First.IsAssignableTo(p.Second));
        return false;
    }

    public override string ToString()
    {
        if (TypeArguments.Count == 0) return BaseType.ToString();
        return $"{BaseType}<{string.Join(", ", TypeArguments)}>";
    }
}
