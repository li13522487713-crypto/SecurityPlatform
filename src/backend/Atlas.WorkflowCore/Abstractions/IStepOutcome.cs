namespace Atlas.WorkflowCore.Abstractions;

public interface IStepOutcome
{
    int NextStep { get; set; }
    string? ExternalNextStepId { get; set; }
    object? GetValue(object? data);
}
