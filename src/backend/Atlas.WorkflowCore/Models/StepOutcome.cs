using Atlas.WorkflowCore.Abstractions;

namespace Atlas.WorkflowCore.Models;

public class ValueOutcome : IStepOutcome
{
    public int NextStep { get; set; }

    public string? ExternalNextStepId { get; set; }

    public object? Value { get; set; }

    public string? Label { get; set; }

    public object? GetValue(object? data)
    {
        return Value;
    }
}
