using System;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Builders;

/// <summary>
/// 容器步骤构建器实现
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
/// <typeparam name="TStepBody">步骤体类型</typeparam>
/// <typeparam name="TReturnStep">返回步骤类型</typeparam>
public class ContainerStepBuilder<TData, TStepBody, TReturnStep> : IContainerStepBuilder<TData, TStepBody, TReturnStep>
    where TStepBody : IStepBody
    where TReturnStep : IStepBody
{
    private readonly IWorkflowBuilder<TData> _workflowBuilder;
    private readonly WorkflowStep _step;
    private readonly IStepBuilder<TData, TReturnStep> _returnStepBuilder;

    public ContainerStepBuilder(
        IWorkflowBuilder<TData> workflowBuilder,
        WorkflowStep step,
        IStepBuilder<TData, TReturnStep> returnStepBuilder)
    {
        _workflowBuilder = workflowBuilder;
        _step = step;
        _returnStepBuilder = returnStepBuilder;
    }

    public IStepBuilder<TData, TReturnStep> Do(Action<IWorkflowBuilder<TData>> builder)
    {
        builder.Invoke(_workflowBuilder);
        _step.Children.Add(_step.Id + 1); //TODO: make more elegant

        return _returnStepBuilder;
    }
}
