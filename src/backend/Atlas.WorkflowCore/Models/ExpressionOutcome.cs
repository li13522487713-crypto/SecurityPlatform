using System;
using System.Linq.Expressions;
using Atlas.WorkflowCore.Abstractions;

namespace Atlas.WorkflowCore.Models;

/// <summary>
/// 表达式结果 - 基于表达式条件的步骤结果
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
public class ExpressionOutcome<TData> : IStepOutcome
{
    private readonly Expression<Func<TData, object?, bool>> _expression;

    /// <summary>
    /// 下一步ID
    /// </summary>
    public int NextStep { get; set; }

    /// <summary>
    /// 外部步骤ID
    /// </summary>
    public string? ExternalNextStepId { get; set; }

    /// <summary>
    /// 分支标签
    /// </summary>
    public string? Label { get; set; }

    public ExpressionOutcome(Expression<Func<TData, object?, bool>> expression)
    {
        _expression = expression;
    }

    public bool Matches(ExecutionResult executionResult, object? data)
    {
        if (data is TData typedData)
        {
            var func = _expression.Compile();
            return func(typedData, executionResult.OutcomeValue);
        }

        return false;
    }

    public bool Matches(object? data)
    {
        if (data is TData typedData)
        {
            var func = _expression.Compile();
            return func(typedData, null);
        }

        return false;
    }
}
