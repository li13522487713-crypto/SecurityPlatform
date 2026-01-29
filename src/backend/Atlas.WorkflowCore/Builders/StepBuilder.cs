using System.Linq.Expressions;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Builders;

public class StepBuilder<TData> : IStepBuilder<TData>
    where TData : new()
{
    public IWorkflowBuilder<TData> WorkflowBuilder { get; private set; }

    public WorkflowStep Step { get; set; }

    public StepBuilder(IWorkflowBuilder<TData> workflowBuilder, WorkflowStep step)
    {
        WorkflowBuilder = workflowBuilder;
        Step = step;
    }

    public IStepBuilder<TData> Input<TInput>(Expression<Func<TData, TInput>> value)
    {
        // 简化实现：存储表达式，后续在运行时解析
        Step.Inputs.Add(new ExpressionStepParameter<TData, TInput>(value));
        return this;
    }

    public IStepBuilder<TData> Input<TInput>(string name, TInput value)
    {
        Step.Inputs.Add(new ConstantStepParameter<TInput>(name, value));
        return this;
    }

    public IStepBuilder<TData> Input<TInput>(string name, Func<TData, TInput> value)
    {
        Step.Inputs.Add(new FuncStepParameter<TData, TInput>(name, value));
        return this;
    }

    public IStepBuilder<TData> Output<TInput>(Expression<Func<TData, TInput>> value, Expression<Func<IStepExecutionContext, TInput>> assign)
    {
        Step.Outputs.Add(new ExpressionStepParameter<IStepExecutionContext, TInput>(assign));
        return this;
    }

    public IStepBuilder<TData> Output<TInput>(string name, Expression<Func<IStepExecutionContext, TInput>> assign)
    {
        Step.Outputs.Add(new ExpressionStepParameter<IStepExecutionContext, TInput>(assign));
        return this;
    }

    public IStepBuilder<TData> Then<TStep>(Action<IStepBuilder<TData>>? stepSetup = null)
        where TStep : IStepBody
    {
        var newStep = new WorkflowStep<TStep>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);
        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);

        stepSetup?.Invoke(stepBuilder);

        newStep.Name = newStep.Name ?? typeof(TStep).Name;
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        return stepBuilder;
    }

    public IStepBuilder<TData> Then(Func<IStepExecutionContext, ExecutionResult> body)
    {
        var newStep = new WorkflowStepInline();
        newStep.Body = body;
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);
        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });
        return stepBuilder;
    }

    public IStepBuilder<TData> Then(string name, Func<IStepExecutionContext, ExecutionResult> body)
    {
        var newStep = new WorkflowStepInline();
        newStep.Name = name;
        newStep.Body = body;
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);
        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });
        return stepBuilder;
    }

    public IWorkflowBuilder<TData> End(string? name = null)
    {
        var endStep = new EndStep();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(endStep);
        Step.Outcomes.Add(new ValueOutcome { NextStep = endStep.Id });
        return WorkflowBuilder;
    }
}
