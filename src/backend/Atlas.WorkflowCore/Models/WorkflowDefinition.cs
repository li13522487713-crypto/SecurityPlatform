namespace Atlas.WorkflowCore.Models;

public class WorkflowDefinition
{
    public string Id { get; set; } = string.Empty;

    public int Version { get; set; }

    public string? Description { get; set; }

    public WorkflowStepCollection Steps { get; set; } = new();

    public Type? DataType { get; set; }

    public WorkflowErrorHandling DefaultErrorBehavior { get; set; }

    public Type? OnPostMiddlewareError { get; set; }

    public Type? OnExecuteMiddlewareError { get; set; }

    public TimeSpan? DefaultErrorRetryInterval { get; set; }
}

public class WorkflowStepCollection : List<WorkflowStep>
{
    public WorkflowStepCollection()
    {
    }

    public WorkflowStepCollection(IEnumerable<WorkflowStep> steps) : base(steps)
    {
    }

    public WorkflowStep? FindById(int id)
    {
        return this.FirstOrDefault(x => x.Id == id);
    }
}
