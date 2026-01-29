using Atlas.WorkflowCore.Abstractions;
using System.Linq.Expressions;

namespace Atlas.WorkflowCore.Models;

public class ExpressionStepParameter<TSource, TValue> : IStepParameter
{
    private readonly Expression<Func<TSource, TValue>> _expression;

    public ExpressionStepParameter(Expression<Func<TSource, TValue>> expression)
    {
        _expression = expression;
    }

    public object? Resolve(object? data)
    {
        if (data is TSource source)
        {
            var compiled = _expression.Compile();
            return compiled(source);
        }
        return null;
    }
}

public class ConstantStepParameter<TValue> : IStepParameter
{
    private readonly string _name;
    private readonly TValue _value;

    public ConstantStepParameter(string name, TValue value)
    {
        _name = name;
        _value = value;
    }

    public object? Resolve(object? data)
    {
        return _value;
    }
}

public class FuncStepParameter<TSource, TValue> : IStepParameter
{
    private readonly string _name;
    private readonly Func<TSource, TValue> _func;

    public FuncStepParameter(string name, Func<TSource, TValue> func)
    {
        _name = name;
        _func = func;
    }

    public object? Resolve(object? data)
    {
        if (data is TSource source)
        {
            return _func(source);
        }
        return null;
    }
}
