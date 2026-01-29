using System;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Builders;

/// <summary>
/// 返回步骤构建器 - 用于容器步骤返回父步骤
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
/// <typeparam name="TStepBody">当前步骤体类型</typeparam>
/// <typeparam name="TParentStep">父步骤类型</typeparam>
public class ReturnStepBuilder<TData, TStepBody, TParentStep> : IContainerStepBuilder<TData, TStepBody, TParentStep>
    where TStepBody : IStepBody
    where TParentStep : IStepBody
{
    private readonly IStepBuilder<TData, TParentStep> _referenceBuilder;

    public IWorkflowBuilder<TData> WorkflowBuilder { get; private set; }

    public WorkflowStep<TStepBody> Step { get; set; }

    public ReturnStepBuilder(IWorkflowBuilder<TData> workflowBuilder, WorkflowStep<TStepBody> step, IStepBuilder<TData, TParentStep> referenceBuilder)
    {
        WorkflowBuilder = workflowBuilder;
        Step = step;
        _referenceBuilder = referenceBuilder;
    }

    public IStepBuilder<TData, TParentStep> Do(Action<IWorkflowBuilder<TData>> builder)
    {
        builder.Invoke(WorkflowBuilder);
        Step.Children.Add(Step.Id + 1); //TODO: make more elegant

        return _referenceBuilder;
    }
}
