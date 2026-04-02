namespace Atlas.Core.Expressions;

/// <summary>
/// 函数注册表 —— 管理内置与自定义函数的签名和求值委托。
/// </summary>
public interface IFunctionRegistry
{
    void Register(FunctionSignature signature, Func<object?[], object?> evaluator);
    bool TryGet(string name, out RegisteredFunction? function);
    IReadOnlyList<FunctionSignature> GetAll();
    IReadOnlyList<FunctionSignature> GetByCategory(FunctionCategory category);
}

/// <summary>
/// 已注册的函数（签名 + 求值委托）。
/// </summary>
public sealed class RegisteredFunction
{
    public required FunctionSignature Signature { get; init; }
    public required Func<object?[], object?> Evaluator { get; init; }
}
