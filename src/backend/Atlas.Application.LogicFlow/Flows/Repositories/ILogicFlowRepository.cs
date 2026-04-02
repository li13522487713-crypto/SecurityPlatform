using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Application.LogicFlow.Flows.Repositories;

public interface ILogicFlowRepository
{
    Task<long> AddAsync(LogicFlowDefinition entity, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(LogicFlowDefinition entity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
    Task<LogicFlowDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<LogicFlowDefinition> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        FlowStatus? status,
        CancellationToken cancellationToken);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);
}

public interface IFlowNodeBindingRepository
{
    Task BulkInsertAsync(IReadOnlyList<FlowNodeBinding> entities, CancellationToken cancellationToken);
    Task BulkDeleteAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<FlowNodeBinding>> GetByFlowIdAsync(long flowDefinitionId, CancellationToken cancellationToken);
    Task DeleteByFlowIdAsync(long flowDefinitionId, CancellationToken cancellationToken);
}

public interface IFlowEdgeRepository
{
    Task BulkInsertAsync(IReadOnlyList<FlowEdgeDefinition> entities, CancellationToken cancellationToken);
    Task BulkDeleteAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<FlowEdgeDefinition>> GetByFlowIdAsync(long flowDefinitionId, CancellationToken cancellationToken);
    Task DeleteByFlowIdAsync(long flowDefinitionId, CancellationToken cancellationToken);
}
