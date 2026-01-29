using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

public interface IWorkflowBuilder<TData>
{
    IStepBuilder<TData> StartWith<TStep>(Action<IStepBuilder<TData>>? stepSetup = null)
        where TStep : IStepBody;

    IStepBuilder<TData> StartWith(Func<IStepExecutionContext, ExecutionResult> body);

    IStepBuilder<TData> StartWith(string name, Func<IStepExecutionContext, ExecutionResult> body);

    IStepBuilder<TData> Then<TStep>(Action<IStepBuilder<TData>>? stepSetup = null)
        where TStep : IStepBody;

    IStepBuilder<TData> Then(Func<IStepExecutionContext, ExecutionResult> body);

    IStepBuilder<TData> Then(string name, Func<IStepExecutionContext, ExecutionResult> body);

    IWorkflowBuilder<TData> If(Func<TData, bool> condition, Action<IWorkflowBuilder<TData>>? branch = null);

    IWorkflowBuilder<TData> While(Func<TData, bool> condition, Action<IWorkflowBuilder<TData>>? body = null);

    IWorkflowBuilder<TData> ForEach(Func<TData, IEnumerable<object>> collection, Action<IStepBuilder<TData>>? stepSetup = null);

    IWorkflowBuilder<TData> Parallel(Action<IParallelStepBuilder<TData>>? parallel);

    IWorkflowBuilder<TData> Saga(Action<IStepBuilder<TData>>? saga);

    IWorkflowBuilder<TData> End(string? name = null);

    WorkflowDefinition Build(string id, int version);
}
