namespace Atlas.WorkflowCore.Abstractions;

public interface IParallelStepBuilder<TData>
{
    IStepBuilder<TData> Do(Action<IStepBuilder<TData>>? branch = null);
}
