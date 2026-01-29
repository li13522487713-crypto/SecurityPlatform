namespace Atlas.WorkflowCore.Models;

public class ExecutionError
{
    public string WorkflowId { get; set; } = string.Empty;

    public string ExecutionPointerId { get; set; } = string.Empty;

    public DateTime ErrorTime { get; set; }

    public string Message { get; set; } = string.Empty;
}
