using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;
using AutoMapper;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class FlowExecutionQueryService : IFlowExecutionQueryService
{
    private readonly IFlowExecutionRepository _executions;
    private readonly INodeRunRepository _nodeRuns;
    private readonly IMapper _mapper;

    public FlowExecutionQueryService(
        IFlowExecutionRepository executions,
        INodeRunRepository nodeRuns,
        IMapper mapper)
    {
        _executions = executions;
        _nodeRuns = nodeRuns;
        _mapper = mapper;
    }

    public async Task<PagedResult<FlowExecutionListItem>> QueryExecutionsAsync(
        long? flowDefId,
        PagedRequest request,
        ExecutionStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _executions.QueryPageAsync(
            tenantId.Value,
            flowDefId,
            status,
            pageIndex,
            pageSize,
            cancellationToken);

        var mapped = items.Select(x => _mapper.Map<FlowExecutionListItem>(x)).ToArray();
        return new PagedResult<FlowExecutionListItem>(mapped, total, pageIndex, pageSize);
    }

    public async Task<FlowExecutionResponse?> GetExecutionByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var execution = await _executions.GetByIdAsync(id, cancellationToken);
        if (execution is null || execution.TenantIdValue != tenantId.Value)
            return null;
        return _mapper.Map<FlowExecutionResponse>(execution);
    }

    public async Task<IReadOnlyList<NodeRunResponse>> GetNodeRunsAsync(
        long executionId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var execution = await _executions.GetByIdAsync(executionId, cancellationToken);
        if (execution is null || execution.TenantIdValue != tenantId.Value)
            return Array.Empty<NodeRunResponse>();

        var runs = await _nodeRuns.GetByExecutionIdAsync(executionId, cancellationToken);
        return runs.Select(x => _mapper.Map<NodeRunResponse>(x)).ToArray();
    }
}
