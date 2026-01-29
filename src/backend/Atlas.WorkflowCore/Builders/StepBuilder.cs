using System.Collections;
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

    public IStepBuilder<TData> Name(string name)
    {
        Step.Name = name;
        return this;
    }

    public IStepBuilder<TData> Id(string id)
    {
        Step.ExternalId = id;
        return this;
    }

    public IStepBuilder<TData> OnError(WorkflowErrorHandling behavior, TimeSpan? retryInterval = null)
    {
        Step.ErrorBehavior = behavior;
        Step.RetryInterval = retryInterval;
        return this;
    }

    public IStepBuilder<TData> CompensateWith<TStep>(Action<IStepBuilder<TData>>? stepSetup = null) where TStep : IStepBody
    {
        var compensationStep = new WorkflowStep<TStep>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(compensationStep);
        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, compensationStep);

        stepSetup?.Invoke(stepBuilder);

        compensationStep.Name = compensationStep.Name ?? $"Compensate {typeof(TStep).Name}";
        Step.CompensationStepId = compensationStep.Id;

        return this;
    }

    public IStepBuilder<TData> CompensateWith(Func<IStepExecutionContext, ExecutionResult> body)
    {
        var compensationStep = new WorkflowStepInline();
        compensationStep.Body = body;
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(compensationStep);
        Step.CompensationStepId = compensationStep.Id;

        return this;
    }

    public IStepBuilder<TData> CompensateWithSequence(Action<IWorkflowBuilder<TData>> builder)
    {
        var branchBuilder = (WorkflowBuilder as WorkflowBuilder<TData>)!.CreateBranch();
        builder(branchBuilder);
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AttachBranch(branchBuilder);

        if (branchBuilder.Steps.Count > 0)
        {
            Step.CompensationStepId = branchBuilder.Steps[0].Id;
        }

        return this;
    }

    public IStepBuilder<TData> CancelCondition(Expression<Func<TData, bool>> cancelCondition, bool proceedAfterCancel = false)
    {
        // TODO: 实现取消条件
        // Step.CancelCondition = cancelCondition;
        // Step.ProceedOnCancel = proceedAfterCancel;
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

    // 控制流方法实现
    public IStepBuilder<TData> WaitFor(string eventName, Expression<Func<TData, string>> eventKey,
        Expression<Func<TData, DateTime>>? effectiveDate = null)
    {
        var newStep = new WorkflowStep<WaitFor>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);
        
        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        
        // 添加输入映射
        newStep.Inputs.Add(new FuncStepParameter<TData, string>("EventName", data => eventName));
        newStep.Inputs.Add(new ExpressionStepParameter<TData, string>(eventKey));
        
        if (effectiveDate != null)
        {
            newStep.Inputs.Add(new ExpressionStepParameter<TData, DateTime>(effectiveDate));
        }

        newStep.Name = $"Wait for {eventName}";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        return stepBuilder;
    }

    public IStepBuilder<TData> Delay(Expression<Func<TData, TimeSpan>> period)
    {
        var newStep = new WorkflowStep<Delay>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        
        // 添加输入映射
        newStep.Inputs.Add(new ExpressionStepParameter<TData, TimeSpan>(period));

        newStep.Name = "Delay";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        return stepBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> Decide(Expression<Func<TData, object>> expression)
    {
        var newStep = new WorkflowStep<Decide>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        // 添加输入映射
        newStep.Inputs.Add(new ExpressionStepParameter<TData, object>(expression));

        newStep.Name = "Decide";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> ForEach(Expression<Func<TData, IEnumerable>> collection)
    {
        var newStep = new WorkflowStep<Foreach>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        // 添加输入映射
        newStep.Inputs.Add(new ExpressionStepParameter<TData, IEnumerable>(collection));

        newStep.Name = "ForEach";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> While(Expression<Func<TData, bool>> condition)
    {
        var newStep = new WorkflowStep<While>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        // 添加输入映射
        newStep.Inputs.Add(new ExpressionStepParameter<TData, bool>(condition));

        newStep.Name = "While";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> If(Expression<Func<TData, bool>> condition)
    {
        var newStep = new WorkflowStep<If>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        // 添加输入映射
        newStep.Inputs.Add(new ExpressionStepParameter<TData, bool>(condition));

        newStep.Name = "If";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IStepBuilder<TData> When(object outcomeValue, string? label = null)
    {
        var newStep = new WorkflowStep<When>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        newStep.Name = label ?? $"When {outcomeValue}";
        Step.Outcomes.Add(new ValueOutcome { Value = outcomeValue, NextStep = newStep.Id, Label = label });

        return stepBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> Parallel()
    {
        var newStep = new WorkflowStep<Sequence>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        newStep.Name = "Parallel";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> Saga()
    {
        var newStep = new WorkflowStep<SagaContainer>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        newStep.Name = "Saga";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> Schedule(Expression<Func<TData, TimeSpan>> time)
    {
        var newStep = new WorkflowStep<Schedule>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        // 添加输入映射
        newStep.Inputs.Add(new ExpressionStepParameter<TData, TimeSpan>(time));

        newStep.Name = "Schedule";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IContainerStepBuilder<TData, IStepBuilder<TData>> Recur(Expression<Func<TData, TimeSpan>> interval,
        Expression<Func<TData, bool>>? until = null)
    {
        var newStep = new WorkflowStep<Recur>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        // 添加输入映射
        newStep.Inputs.Add(new ExpressionStepParameter<TData, TimeSpan>(interval));

        if (until != null)
        {
            newStep.Inputs.Add(new ExpressionStepParameter<TData, bool>(until));
        }

        newStep.Name = "Recur";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        var containerBuilder = new ContainerStepBuilder<TData, IStepBuilder<TData>>(WorkflowBuilder, newStep, stepBuilder);

        return containerBuilder;
    }

    public IStepBuilder<TData> Activity(string activityName, Expression<Func<TData, object>>? parameters = null,
        Expression<Func<TData, DateTime>>? effectiveDate = null)
    {
        var newStep = new WorkflowStep<Activity>();
        (WorkflowBuilder as WorkflowBuilder<TData>)!.AddStep(newStep);

        var stepBuilder = new StepBuilder<TData>(WorkflowBuilder, newStep);
        
        // 添加输入映射
        newStep.Inputs.Add(new FuncStepParameter<TData, string>("ActivityName", data => activityName));

        if (parameters != null)
        {
            newStep.Inputs.Add(new ExpressionStepParameter<TData, object>(parameters));
        }

        if (effectiveDate != null)
        {
            newStep.Inputs.Add(new ExpressionStepParameter<TData, DateTime>(effectiveDate));
        }

        newStep.Name = $"Activity {activityName}";
        Step.Outcomes.Add(new ValueOutcome { NextStep = newStep.Id });

        return stepBuilder;
    }
}
