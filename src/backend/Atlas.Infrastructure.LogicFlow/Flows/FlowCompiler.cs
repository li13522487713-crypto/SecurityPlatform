using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.LogicFlow.Flows;

public sealed class FlowCompiler : IFlowCompiler
{
    private readonly ILogicFlowRepository _flowRepository;
    private readonly IFlowNodeBindingRepository _nodeRepository;
    private readonly IFlowEdgeRepository _edgeRepository;

    public FlowCompiler(
        ILogicFlowRepository flowRepository,
        IFlowNodeBindingRepository nodeRepository,
        IFlowEdgeRepository edgeRepository)
    {
        _flowRepository = flowRepository;
        _nodeRepository = nodeRepository;
        _edgeRepository = edgeRepository;
    }

    public async Task<PhysicalDagPlan> CompileAsync(long flowId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        if (flow.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        var bindings = await _nodeRepository.GetByFlowIdAsync(flowId, cancellationToken);
        var edges = await _edgeRepository.GetByFlowIdAsync(flowId, cancellationToken);

        var deps = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var b in bindings)
            deps[b.NodeInstanceKey] = [];

        foreach (var e in edges)
        {
            if (!deps.ContainsKey(e.TargetNodeKey))
                deps[e.TargetNodeKey] = [];
            deps[e.TargetNodeKey].Add(e.SourceNodeKey);
        }

        var nodes = bindings.Select(b => new PhysicalDagNode
        {
            NodeKey = b.NodeInstanceKey,
            TypeKey = b.NodeTypeKey,
            ConfigJson = string.IsNullOrWhiteSpace(b.ConfigJson) ? "{}" : b.ConfigJson,
            Dependencies = deps.TryGetValue(b.NodeInstanceKey, out var d) ? d.Distinct(StringComparer.Ordinal).ToList() : [],
        }).ToList();

        var planEdges = edges.Select(e => new PhysicalDagEdge
        {
            SourceNodeKey = e.SourceNodeKey,
            SourcePortKey = e.SourcePortKey,
            TargetNodeKey = e.TargetNodeKey,
            TargetPortKey = e.TargetPortKey,
        }).ToList();

        return new PhysicalDagPlan
        {
            FlowId = flowId,
            Nodes = nodes,
            Edges = planEdges,
        };
    }
}
