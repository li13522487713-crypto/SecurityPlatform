namespace Atlas.WorkflowCore.Abstractions;

public interface IWorkflow<TData>
{
    string Id { get; }
    int Version { get; }
    void Build(IWorkflowBuilder<TData> builder);
}

public interface IWorkflow : IWorkflow<object>
{
}
