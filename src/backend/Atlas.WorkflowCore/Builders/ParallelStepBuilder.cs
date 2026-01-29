using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Builders;

public class ParallelStepBuilder<TData> : IParallelStepBuilder<TData>
    where TData : new()
{
    private readonly IWorkflowBuilder<TData> _workflowBuilder;
    private readonly IStepBuilder<TData> _stepBuilder;

    public ParallelStepBuilder(IWorkflowBuilder<TData> workflowBuilder, IStepBuilder<TData> stepBuilder)
    {
        _workflowBuilder = workflowBuilder;
        _stepBuilder = stepBuilder;
    }

    public IStepBuilder<TData> Do(Action<IStepBuilder<TData>>? branch = null)
    {
        branch?.Invoke(_stepBuilder);
        return _stepBuilder;
    }
}
