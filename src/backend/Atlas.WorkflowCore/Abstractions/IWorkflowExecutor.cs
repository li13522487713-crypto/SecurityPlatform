using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

public interface IWorkflowExecutor
{
    Task<WorkflowExecutorResult> Execute(WorkflowInstance workflow, CancellationToken cancellationToken = default);
}
