namespace Atlas.WorkflowCore.Models;

public class Event
{
    public const string EventTypeActivity = "Activity";

    public string Id { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public string EventKey { get; set; } = string.Empty;

    public object? EventData { get; set; }

    public DateTime EventTime { get; set; }

    public bool IsProcessed { get; set; }
}
