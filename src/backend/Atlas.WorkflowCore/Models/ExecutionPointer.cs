namespace Atlas.WorkflowCore.Models;

public class ExecutionPointer
{
    private IReadOnlyCollection<string> _scope = new List<string>();

    public string Id { get; set; } = string.Empty;

    public int StepId { get; set; }

    public bool Active { get; set; }

    public DateTime? SleepUntil { get; set; }

    public object? PersistenceData { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? EventName { get; set; }

    public string? EventKey { get; set; }

    public bool EventPublished { get; set; }

    public object? EventData { get; set; }

    public Dictionary<string, object> ExtensionAttributes { get; set; } = new();

    public string? StepName { get; set; }

    public int RetryCount { get; set; }

    public List<string> Children { get; set; } = new();

    public object? ContextItem { get; set; }

    public string? PredecessorId { get; set; }

    public object? Outcome { get; set; }

    public PointerStatus Status { get; set; } = PointerStatus.Legacy;

    public IReadOnlyCollection<string> Scope
    {
        get => _scope;
        set => _scope = new List<string>(value);
    }
}
