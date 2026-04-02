using Atlas.Application.LogicFlow.Flows.Models;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface INodeExecutor
{
    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken);
}
