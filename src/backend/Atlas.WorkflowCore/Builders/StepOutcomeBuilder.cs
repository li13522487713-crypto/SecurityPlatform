using System;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Builders;

/// <summary>
/// 步骤结果构建器实现
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
public class StepOutcomeBuilder<TData> : IStepOutcomeBuilder<TData>
{
    public IWorkflowBuilder<TData> WorkflowBuilder { get; private set; }
    
    public ValueOutcome Outcome { get; private set; }

    public StepOutcomeBuilder(IWorkflowBuilder<TData> workflowBuilder, ValueOutcome outcome)
    {
        WorkflowBuilder = workflowBuilder ?? throw new ArgumentNullException(nameof(workflowBuilder));
        Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
    }

    public IStepBuilder<TData, TStep> Then<TStep>(Action<IStepBuilder<TData, TStep>>? stepSetup = null)
        where TStep : IStepBody
    {
        var step = new WorkflowStep<TStep>();
        WorkflowBuilder.AddStep(step);
        var stepBuilder = new StepBuilder<TData, TStep>(WorkflowBuilder, step);

        stepSetup?.Invoke(stepBuilder);

        step.Name = step.Name ?? typeof(TStep).Name;
        Outcome.NextStep = step.Id;

        return stepBuilder;
    }

    public IStepBuilder<TData, TStep> Then<TStep>(IStepBuilder<TData, TStep> step)
        where TStep : IStepBody
    {
        Outcome.NextStep = step.Step.Id;
        var stepBuilder = new StepBuilder<TData, TStep>(WorkflowBuilder, step.Step);
        return stepBuilder;
    }

    public IStepBuilder<TData, InlineStepBody> Then(Func<IStepExecutionContext, ExecutionResult> body)
    {
        var newStep = new WorkflowStepInline();
        newStep.Body = body;
        WorkflowBuilder.AddStep(newStep);
        var stepBuilder = new StepBuilder<TData, InlineStepBody>(WorkflowBuilder, newStep);
        Outcome.NextStep = newStep.Id;
        return stepBuilder;
    }

    public void EndWorkflow()
    {
        var newStep = new EndStep();
        WorkflowBuilder.AddStep(newStep);
        Outcome.NextStep = newStep.Id;
    }
}
