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

    /// <summary>
    /// 根据外部ID查找步骤
    /// </summary>
    public WorkflowStep? FindByExternalId(string externalId)
    {
        return this.FirstOrDefault(s => s.ExternalId == externalId);
    }

    /// <summary>
    /// 根据名称查找步骤
    /// </summary>
    public WorkflowStep? FindByName(string name)
    {
        return this.FirstOrDefault(s => s.Name == name);
    }

    /// <summary>
    /// 根据名称查找所有匹配的步骤
    /// </summary>
    public List<WorkflowStep> FindAllByName(string name)
    {
        return this.Where(s => s.Name == name).ToList();
    }

    /// <summary>
    /// 获取所有根步骤（没有前置步骤的步骤）
    /// </summary>
    public List<WorkflowStep> GetRootSteps()
    {
        var allNextStepIds = this.SelectMany(s => s.Outcomes)
            .Select(o => o.NextStep)
            .Where(id => id > 0)
            .Distinct()
            .ToHashSet();

        return this.Where(s => !allNextStepIds.Contains(s.Id)).ToList();
    }

    /// <summary>
    /// 获取指定步骤的所有子步骤
    /// </summary>
    public List<WorkflowStep> GetChildSteps(int parentStepId)
    {
        var parentStep = FindById(parentStepId);
        if (parentStep == null)
        {
            return new List<WorkflowStep>();
        }

        return parentStep.Children
            .Select(childId => FindById(childId))
            .Where(s => s != null)
            .Cast<WorkflowStep>()
            .ToList();
    }

    /// <summary>
    /// 获取指定步骤的所有后续步骤
    /// </summary>
    public List<WorkflowStep> GetNextSteps(int stepId)
    {
        var step = FindById(stepId);
        if (step == null)
        {
            return new List<WorkflowStep>();
        }

        return step.Outcomes
            .Where(o => o.NextStep > 0)
            .Select(o => FindById(o.NextStep))
            .Where(s => s != null)
            .Cast<WorkflowStep>()
            .ToList();
    }

    /// <summary>
    /// 检查步骤是否存在
    /// </summary>
    public bool Contains(int stepId)
    {
        return this.Any(s => s.Id == stepId);
    }

    /// <summary>
    /// 移除步骤
    /// </summary>
    public bool RemoveById(int id)
    {
        var step = FindById(id);
        if (step != null)
        {
            return Remove(step);
        }
        return false;
    }
}
