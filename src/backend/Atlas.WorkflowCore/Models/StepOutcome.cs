using System;
using System.Linq.Expressions;
using Atlas.WorkflowCore.Abstractions;

namespace Atlas.WorkflowCore.Models;

/// <summary>
/// 值结果 - 基于静态值的步骤结果
/// </summary>
public class ValueOutcome : IStepOutcome
{
    private LambdaExpression? _value;

    public LambdaExpression? Value
    {
        get { return _value; }
        set { _value = value; }
    }

    public int NextStep { get; set; }

    public string? Label { get; set; }

    public string? ExternalNextStepId { get; set; }

    public bool Matches(ExecutionResult executionResult, object? data)
    {
        return object.Equals(GetValue(data), executionResult.OutcomeValue) || GetValue(data) == null;
    }

    public bool Matches(object? data)
    {
        return GetValue(data) == null;
    }

    public object? GetValue(object? data)
    {
        if (_value == null)
            return null;

        return _value.Compile().DynamicInvoke(data);
    }
}
