using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Application.LogicFlow.Flows.Repositories;

public interface IFlowExecutionRepository
{
    Task<long> AddAsync(FlowExecution entity, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(FlowExecution entity, CancellationToken cancellationToken);
    Task<FlowExecution?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<FlowExecution> Items, int TotalCount)> QueryPageAsync(
        Guid tenantId,
        long? flowDefinitionId,
        ExecutionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
    Task<FlowExecution?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
}

public interface INodeRunRepository
{
    Task<long> AddAsync(NodeRun entity, CancellationToken cancellationToken);
    Task BulkInsertAsync(IReadOnlyList<NodeRun> entities, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(NodeRun entity, CancellationToken cancellationToken);
    Task<NodeRun?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<NodeRun>> GetByExecutionIdAsync(long flowExecutionId, CancellationToken cancellationToken);
    Task<NodeRun?> GetByExecutionIdAndNodeKeyAsync(long flowExecutionId, string nodeKey, CancellationToken cancellationToken);
}
