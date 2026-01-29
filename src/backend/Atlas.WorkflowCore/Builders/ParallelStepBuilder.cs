using System;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Builders;

/// <summary>
/// 并行步骤构建器实现
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
/// <typeparam name="TStepBody">步骤体类型</typeparam>
public class ParallelStepBuilder<TData, TStepBody> : IParallelStepBuilder<TData, TStepBody>
    where TStepBody : IStepBody
{
    private readonly IStepBuilder<TData, Sequence> _referenceBuilder;
    private readonly IStepBuilder<TData, TStepBody> _stepBuilder;

    public IWorkflowBuilder<TData> WorkflowBuilder { get; private set; }

    public WorkflowStep<TStepBody> Step { get; set; }

    public ParallelStepBuilder(IWorkflowBuilder<TData> workflowBuilder, IStepBuilder<TData, TStepBody> stepBuilder, IStepBuilder<TData, Sequence> referenceBuilder)
    {
        WorkflowBuilder = workflowBuilder;
        Step = stepBuilder.Step;
        _stepBuilder = stepBuilder;
        _referenceBuilder = referenceBuilder;
    }

    public IStepBuilder<TData, TStepBody> Do(Action<IStepBuilder<TData, InlineStepBody>>? branch = null)
    {
        if (branch != null)
        {
            var lastStep = WorkflowBuilder.LastStep;
            var inlineStep = new WorkflowStepInline();
            WorkflowBuilder.AddStep(inlineStep);
            var stepBuilder = new StepBuilder<TData, InlineStepBody>(WorkflowBuilder, inlineStep);
            branch.Invoke(stepBuilder);

            if (lastStep != WorkflowBuilder.LastStep)
            {
                Step.Children.Add(lastStep + 1);
            }
        }

        return _stepBuilder;
    }

    public IStepBuilder<TData, Sequence> Join()
    {
        return _referenceBuilder;
    }
}
