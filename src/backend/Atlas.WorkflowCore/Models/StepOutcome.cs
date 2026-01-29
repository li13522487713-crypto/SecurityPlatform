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

    /// <summary>
    /// 匹配结果值
    /// </summary>
    public bool Matches(object? data, object? outcomeValue)
    {
        if (Value == null && outcomeValue == null)
        {
            return true;
        }

        if (Value != null && outcomeValue != null)
        {
            return Value.Equals(outcomeValue);
        }

        return false;
    }
}
