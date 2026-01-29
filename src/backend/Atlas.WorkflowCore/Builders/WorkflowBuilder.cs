using System.Linq.Expressions;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Builders;

public class WorkflowBuilder<TData> : IWorkflowBuilder<TData>
    where TData : new()
{
    public List<WorkflowStep> Steps { get; set; } = new();

    protected WorkflowErrorHandling DefaultErrorBehavior = WorkflowErrorHandling.Retry;

    protected TimeSpan? DefaultErrorRetryInterval;

    public int LastStep => Steps.Count > 0 ? Steps.Max(x => x.Id) : -1;

    public WorkflowDefinition Build(string id, int version)
    {
        AttachExternalIds();
        return new WorkflowDefinition
        {
            Id = id,
            Version = version,
            Steps = new WorkflowStepCollection(Steps),
            DefaultErrorBehavior = DefaultErrorBehavior,
            DefaultErrorRetryInterval = DefaultErrorRetryInterval,
            DataType = typeof(TData)
        };
    }

    public void AddStep(WorkflowStep step)
    {
        step.Id = Steps.Count;
        Steps.Add(step);
    }

    private void AttachExternalIds()
    {
        foreach (var step in Steps)
        {
            foreach (var outcome in step.Outcomes.Where(x => !string.IsNullOrEmpty(x.ExternalNextStepId)))
            {
                if (Steps.All(x => x.ExternalId != outcome.ExternalNextStepId))
                {
                    throw new KeyNotFoundException($"Cannot find step id {outcome.ExternalNextStepId}");
                }

                outcome.NextStep = Steps.Single(x => x.ExternalId == outcome.ExternalNextStepId).Id;
            }
        }
    }

    public IStepBuilder<TData> StartWith<TStep>(Action<IStepBuilder<TData>>? stepSetup = null)
        where TStep : IStepBody
    {
        var step = new WorkflowStep<TStep>();
        var stepBuilder = new StepBuilder<TData>(this, step);

        stepSetup?.Invoke(stepBuilder);

        step.Name = step.Name ?? typeof(TStep).Name;
        AddStep(step);
        return stepBuilder;
    }

    public IStepBuilder<TData> StartWith(Func<IStepExecutionContext, ExecutionResult> body)
    {
        var newStep = new WorkflowStepInline();
        newStep.Body = body;
        var stepBuilder = new StepBuilder<TData>(this, newStep);
        AddStep(newStep);
        return stepBuilder;
    }

    public IStepBuilder<TData> StartWith(string name, Func<IStepExecutionContext, ExecutionResult> body)
    {
        var newStep = new WorkflowStepInline();
        newStep.Name = name;
        newStep.Body = body;
        var stepBuilder = new StepBuilder<TData>(this, newStep);
        AddStep(newStep);
        return stepBuilder;
    }

    public IStepBuilder<TData> Then<TStep>(Action<IStepBuilder<TData>>? stepSetup = null)
        where TStep : IStepBody
    {
        return Start().Then<TStep>(stepSetup);
    }

    public IStepBuilder<TData> Then(Func<IStepExecutionContext, ExecutionResult> body)
    {
        return Start().Then(body);
    }

    public IStepBuilder<TData> Then(string name, Func<IStepExecutionContext, ExecutionResult> body)
    {
        return Start().Then(name, body);
    }

    public IWorkflowBuilder<TData> If(Func<TData, bool> condition, Action<IWorkflowBuilder<TData>>? branch = null)
    {
        var ifStep = new WorkflowStep<Primitives.If>();
        AddStep(ifStep);
        var stepBuilder = new StepBuilder<TData>(this, ifStep);
        stepBuilder.Input(nameof(Primitives.If.Condition), (TData data) => condition(data));

        if (branch != null)
        {
            var branchBuilder = CreateBranch();
            branch(branchBuilder);
            AttachBranch(branchBuilder);
            if (branchBuilder.Steps.Count > 0)
            {
                ifStep.Children.Add(branchBuilder.Steps[0].Id);
            }
        }

        return this;
    }

    public IWorkflowBuilder<TData> While(Func<TData, bool> condition, Action<IWorkflowBuilder<TData>>? body = null)
    {
        var whileStep = new WorkflowStep<Primitives.While>();
        AddStep(whileStep);
        var stepBuilder = new StepBuilder<TData>(this, whileStep);
        stepBuilder.Input(nameof(Primitives.While.Condition), (TData data) => condition(data));

        if (body != null)
        {
            var branchBuilder = CreateBranch();
            body(branchBuilder);
            AttachBranch(branchBuilder);
            if (branchBuilder.Steps.Count > 0)
            {
                whileStep.Children.Add(branchBuilder.Steps[0].Id);
            }
        }

        return this;
    }

    public IWorkflowBuilder<TData> ForEach(Func<TData, IEnumerable<object>> collection, Action<IStepBuilder<TData>>? stepSetup = null)
    {
        var foreachStep = new WorkflowStep<Primitives.Foreach>();
        AddStep(foreachStep);
        var stepBuilder = new StepBuilder<TData>(this, foreachStep);
        stepBuilder.Input(nameof(Primitives.Foreach.Collection), collection);

        stepSetup?.Invoke(stepBuilder);

        return this;
    }

    public IWorkflowBuilder<TData> Parallel(Action<IParallelStepBuilder<TData>>? parallel)
    {
        var sequenceStep = new WorkflowStep<Primitives.Sequence>();
        AddStep(sequenceStep);
        var stepBuilder = new StepBuilder<TData>(this, sequenceStep);

        parallel?.Invoke(new ParallelStepBuilder<TData>(this, stepBuilder));

        return this;
    }

    public IWorkflowBuilder<TData> Saga(Action<IStepBuilder<TData>>? saga)
    {
        var sequenceStep = new WorkflowStep<Primitives.Sequence>();
        AddStep(sequenceStep);
        var stepBuilder = new StepBuilder<TData>(this, sequenceStep);

        saga?.Invoke(stepBuilder);

        return this;
    }

    public IWorkflowBuilder<TData> End(string? name = null)
    {
        var endStep = new EndStep();
        AddStep(endStep);
        return this;
    }

    private IStepBuilder<TData> Start()
    {
        return StartWith(_ => ExecutionResult.Next());
    }

    private WorkflowBuilder<TData> CreateBranch()
    {
        return new WorkflowBuilder<TData>();
    }

    private void AttachBranch(WorkflowBuilder<TData> branch)
    {
        if (branch.Steps.Count == 0)
        {
            return;
        }

        var branchStart = LastStep + 1;

        foreach (var step in branch.Steps)
        {
            var oldId = step.Id;
            step.Id = oldId + branchStart;

            foreach (var step2 in branch.Steps)
            {
                foreach (var outcome in step2.Outcomes)
                {
                    if (outcome.NextStep == oldId)
                    {
                        outcome.NextStep = step.Id;
                    }
                }

                for (var i = 0; i < step2.Children.Count; i++)
                {
                    if (step2.Children[i] == oldId)
                    {
                        step2.Children[i] = step.Id;
                    }
                }

                if (step2.CompensationStepId == oldId)
                {
                    step2.CompensationStepId = step.Id;
                }
            }
        }

        foreach (var step in branch.Steps)
        {
            AddStep(step);
        }
    }
}
