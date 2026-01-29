using System.Threading;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

public interface IStepExecutionContext
{
    object? Item { get; set; }

    ExecutionPointer ExecutionPointer { get; set; }

    object? PersistenceData { get; set; }

    WorkflowStep Step { get; set; }

    WorkflowInstance Workflow { get; set; }

    CancellationToken CancellationToken { get; set; }
}
