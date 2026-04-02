using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;
using AutoMapper;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class LogicFlowQueryService : ILogicFlowQueryService
{
    private readonly ILogicFlowRepository _flowRepository;
    private readonly IFlowNodeBindingRepository _nodeRepository;
    private readonly IFlowEdgeRepository _edgeRepository;
    private readonly IMapper _mapper;

    public LogicFlowQueryService(
        ILogicFlowRepository flowRepository,
        IFlowNodeBindingRepository nodeRepository,
        IFlowEdgeRepository edgeRepository,
        IMapper mapper)
    {
        _flowRepository = flowRepository;
        _nodeRepository = nodeRepository;
        _edgeRepository = edgeRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<LogicFlowListItem>> QueryAsync(
        PagedRequest request,
        FlowStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _flowRepository.QueryPageAsync(
            pageIndex,
            pageSize,
            request.Keyword,
            status,
            cancellationToken);

        var mapped = items.Select(x => _mapper.Map<LogicFlowListItem>(x)).ToArray();
        return new PagedResult<LogicFlowListItem>(mapped, total, pageIndex, pageSize);
    }

    public async Task<LogicFlowDetailResponse?> GetByIdAsync(
        long id,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var flow = await _flowRepository.GetByIdAsync(id, cancellationToken);
        if (flow is null || flow.TenantIdValue != tenantId.Value)
            return null;

        var nodes = await _nodeRepository.GetByFlowIdAsync(id, cancellationToken);
        var edges = await _edgeRepository.GetByFlowIdAsync(id, cancellationToken);

        var detail = _mapper.Map<LogicFlowDetailResponse>(flow);
        detail.Nodes = nodes.Select(n => _mapper.Map<FlowNodeBindingResponse>(n)).ToList();
        detail.Edges = edges.Select(e => _mapper.Map<FlowEdgeResponse>(e)).ToList();
        return detail;
    }
}
