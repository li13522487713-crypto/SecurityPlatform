using System.Threading;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services;

public class StepExecutionContext : IStepExecutionContext
{
    public object? Item { get; set; }

    public ExecutionPointer ExecutionPointer { get; set; } = null!;

    public object? PersistenceData { get; set; }

    public WorkflowStep Step { get; set; } = null!;

    public WorkflowInstance Workflow { get; set; } = null!;

    public CancellationToken CancellationToken { get; set; }
}
