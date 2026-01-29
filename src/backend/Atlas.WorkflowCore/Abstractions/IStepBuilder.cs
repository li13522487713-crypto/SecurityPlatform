using System.Linq.Expressions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

public interface IStepBuilder<TData>
{
    IStepBuilder<TData> Input<TInput>(Expression<Func<TData, TInput>> value);

    IStepBuilder<TData> Input<TInput>(string name, TInput value);

    IStepBuilder<TData> Output<TInput>(Expression<Func<TData, TInput>> value, Expression<Func<IStepExecutionContext, TInput>> assign);

    IStepBuilder<TData> Output<TInput>(string name, Expression<Func<IStepExecutionContext, TInput>> assign);

    IStepBuilder<TData> Then<TStep>(Action<IStepBuilder<TData>>? stepSetup = null)
        where TStep : IStepBody;

    IStepBuilder<TData> Then(Func<IStepExecutionContext, ExecutionResult> body);

    IStepBuilder<TData> Then(string name, Func<IStepExecutionContext, ExecutionResult> body);

    IWorkflowBuilder<TData> End(string? name = null);
}
