namespace Atlas.WorkflowCore.Abstractions;

public interface IStepParameter
{
    object? Resolve(object? data);
}
