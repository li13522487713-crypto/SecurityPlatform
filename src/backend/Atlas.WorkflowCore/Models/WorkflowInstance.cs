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

    public List<ExecutionError> ExecutionErrors { get; set; } = new();

    public bool IsBranchComplete(string parentId)
    {
        return ExecutionPointers
            .FindByScope(parentId)
            .All(x => x.EndTime != null);
    }
}

public class ExecutionPointerCollection : List<ExecutionPointer>
{
    public ExecutionPointerCollection()
    {
    }

    public ExecutionPointerCollection(IEnumerable<ExecutionPointer> collection) : base(collection)
    {
    }

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

    /// <summary>
    /// 根据步骤ID查找所有执行指针
    /// </summary>
    public List<ExecutionPointer> FindByStepId(int stepId)
    {
        return this.Where(p => p.StepId == stepId).ToList();
    }

    /// <summary>
    /// 获取所有活动的执行指针（状态为Running或Pending）
    /// </summary>
    public List<ExecutionPointer> GetActivePointers()
    {
        return this.Where(p => 
            p.Status == PointerStatus.Running || 
            p.Status == PointerStatus.Pending).ToList();
    }

    /// <summary>
    /// 获取所有等待的执行指针（状态为WaitingForEvent）
    /// </summary>
    public List<ExecutionPointer> GetWaitingPointers()
    {
        return this.Where(p => p.Status == PointerStatus.WaitingForEvent).ToList();
    }

    /// <summary>
    /// 获取所有已完成的执行指针
    /// </summary>
    public List<ExecutionPointer> GetCompletedPointers()
    {
        return this.Where(p => p.Status == PointerStatus.Complete).ToList();
    }

    /// <summary>
    /// 获取所有失败的执行指针
    /// </summary>
    public List<ExecutionPointer> GetFailedPointers()
    {
        return this.Where(p => p.Status == PointerStatus.Failed).ToList();
    }

    /// <summary>
    /// 检查是否存在指定状态的指针
    /// </summary>
    public bool HasPointersWithStatus(PointerStatus status)
    {
        return this.Any(p => p.Status == status);
    }

    /// <summary>
    /// 根据状态查找执行指针
    /// </summary>
    public IEnumerable<ExecutionPointer> FindByStatus(PointerStatus status)
    {
        return this.Where(x => x.Status == status);
    }
}
