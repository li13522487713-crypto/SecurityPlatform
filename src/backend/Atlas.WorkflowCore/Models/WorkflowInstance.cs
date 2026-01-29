using System.Linq;

namespace Atlas.WorkflowCore.Models;

public class WorkflowInstance
{
    public string Id { get; set; } = string.Empty;

    public string WorkflowDefinitionId { get; set; } = string.Empty;

    public int Version { get; set; }

    public string? Description { get; set; }

    public string? Reference { get; set; }

    public ExecutionPointerCollection ExecutionPointers { get; set; } = new();

    public long? NextExecution { get; set; }

    public WorkflowStatus Status { get; set; }

    public object? Data { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime? CompleteTime { get; set; }

    public bool IsBranchComplete(string parentId)
    {
        return ExecutionPointers
            .FindByScope(parentId)
            .All(x => x.EndTime != null);
    }
}

public class ExecutionPointerCollection : List<ExecutionPointer>
{
    public IEnumerable<ExecutionPointer> FindByScope(string scope)
    {
        return this.Where(x => x.Scope.Contains(scope));
    }

    public ExecutionPointer? FindById(string id)
    {
        return this.FirstOrDefault(x => x.Id == id);
    }

    public IEnumerable<ExecutionPointer> FindActive()
    {
        return this.Where(x => x.Active && x.EndTime == null);
    }
}
