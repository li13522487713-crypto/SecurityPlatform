using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Functions;

/// <summary>
/// 内置函数注册表 —— 注册所有内置字符串/数值/日期/转换/集合/聚合/窗口函数。
/// </summary>
public sealed class BuiltinFunctionRegistry : IFunctionRegistry
{
    private readonly Dictionary<string, RegisteredFunction> _functions = new(StringComparer.OrdinalIgnoreCase);

    public BuiltinFunctionRegistry()
    {
        StringFunctions.Register(this);
        NumericFunctions.Register(this);
        DateFunctions.Register(this);
        ConversionFunctions.Register(this);
        CollectionFunctions.Register(this);
        AggregateFunctions.Register(this);
        WindowFunctions.Register(this);
    }

    public void Register(FunctionSignature signature, Func<object?[], object?> evaluator)
    {
        _functions[signature.Name] = new RegisteredFunction { Signature = signature, Evaluator = evaluator };
    }

    public bool TryGet(string name, out RegisteredFunction? function)
        => _functions.TryGetValue(name, out function);

    public IReadOnlyList<FunctionSignature> GetAll()
        => _functions.Values.Select(f => f.Signature).ToList();

    public IReadOnlyList<FunctionSignature> GetByCategory(FunctionCategory category)
        => _functions.Values.Where(f => f.Signature.Category == category).Select(f => f.Signature).ToList();
}
