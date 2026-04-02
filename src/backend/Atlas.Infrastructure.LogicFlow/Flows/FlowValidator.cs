using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;
using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Infrastructure.LogicFlow.Flows;

public sealed class FlowValidator : IFlowValidator
{
    private readonly ILogicFlowRepository _flowRepository;
    private readonly IFlowNodeBindingRepository _nodeRepository;
    private readonly IFlowEdgeRepository _edgeRepository;
    private readonly INodeTypeRegistry _nodeTypeRegistry;
    private readonly INodeTypeRepository _nodeTypeRepository;

    public FlowValidator(
        ILogicFlowRepository flowRepository,
        IFlowNodeBindingRepository nodeRepository,
        IFlowEdgeRepository edgeRepository,
        INodeTypeRegistry nodeTypeRegistry,
        INodeTypeRepository nodeTypeRepository)
    {
        _flowRepository = flowRepository;
        _nodeRepository = nodeRepository;
        _edgeRepository = edgeRepository;
        _nodeTypeRegistry = nodeTypeRegistry;
        _nodeTypeRepository = nodeTypeRepository;
    }

    public async Task<FlowValidationResult> ValidateAsync(long flowId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var errors = new List<FlowValidationError>();
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken);
        if (flow is null || flow.TenantIdValue != tenantId.Value)
        {
            errors.Add(new FlowValidationError
            {
                Code = "FLOW_NOT_FOUND",
                Message = "逻辑流不存在或无权访问",
            });
            return new FlowValidationResult { IsValid = false, Errors = errors };
        }

        var nodes = (await _nodeRepository.GetByFlowIdAsync(flowId, cancellationToken)).ToList();
        var edges = (await _edgeRepository.GetByFlowIdAsync(flowId, cancellationToken)).ToList();

        if (nodes.Count == 0)
        {
            errors.Add(new FlowValidationError { Code = "NO_NODES", Message = "流程未包含任何节点" });
            return Finish(errors);
        }

        var nodeByKey = nodes.ToDictionary(n => n.NodeInstanceKey, StringComparer.Ordinal);
        var nodeKeys = nodeByKey.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var e in edges)
        {
            if (!nodeKeys.Contains(e.SourceNodeKey))
            {
                errors.Add(new FlowValidationError
                {
                    Code = "EDGE_UNKNOWN_SOURCE",
                    Message = $"边引用了不存在的源节点 '{e.SourceNodeKey}'",
                    NodeKey = e.SourceNodeKey,
                });
            }

            if (!nodeKeys.Contains(e.TargetNodeKey))
            {
                errors.Add(new FlowValidationError
                {
                    Code = "EDGE_UNKNOWN_TARGET",
                    Message = $"边引用了不存在的目标节点 '{e.TargetNodeKey}'",
                    NodeKey = e.TargetNodeKey,
                });
            }
        }

        var forward = nodeKeys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (nodeKeys.Contains(e.SourceNodeKey) && nodeKeys.Contains(e.TargetNodeKey))
                forward[e.SourceNodeKey].Add(e.TargetNodeKey);
        }

        var triggerTypeKeys = _nodeTypeRegistry.GetByCategory(NodeCategory.Trigger)
            .Select(d => d.TypeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entryKeys = nodes
            .Where(n => triggerTypeKeys.Contains(n.NodeTypeKey))
            .Select(n => n.NodeInstanceKey)
            .ToHashSet(StringComparer.Ordinal);

        if (entryKeys.Count == 0)
        {
            var inDegree = nodeKeys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
            foreach (var e in edges)
            {
                if (nodeKeys.Contains(e.TargetNodeKey))
                    inDegree[e.TargetNodeKey]++;
            }

            entryKeys = nodeKeys.Where(k => inDegree[k] == 0).ToHashSet(StringComparer.Ordinal);
        }

        if (entryKeys.Count == 0)
        {
            errors.Add(new FlowValidationError { Code = "NO_ENTRY", Message = "无法确定流程入口节点（无触发节点且无零入边节点）" });
        }
        else
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var q = new Queue<string>(entryKeys);
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                if (!reachable.Add(u))
                    continue;
                foreach (var v in forward[u])
                {
                    if (!reachable.Contains(v))
                        q.Enqueue(v);
                }
            }

            foreach (var k in nodeKeys)
            {
                if (!reachable.Contains(k))
                {
                    errors.Add(new FlowValidationError
                    {
                        Code = "UNREACHABLE",
                        Message = $"节点 '{k}' 无法从入口到达",
                        NodeKey = k,
                    });
                }
            }
        }

        var color = nodeKeys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
        foreach (var start in nodeKeys)
        {
            if (color[start] != 0)
                continue;
            if (DfsCycle(start, forward, color, errors))
                break;
        }

        var typeKeysNeedingDb = nodes
            .Select(n => n.NodeTypeKey)
            .Where(t => _nodeTypeRegistry.GetDeclaration(t) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fromDb = typeKeysNeedingDb.Count == 0
            ? new List<NodeTypeDefinition>()
            : (await _nodeTypeRepository.GetManyByTypeKeysAsync(typeKeysNeedingDb, tenantId, cancellationToken)).ToList();

        var dbTypeMap = fromDb.ToDictionary(x => x.TypeKey, StringComparer.OrdinalIgnoreCase);

        foreach (var e in edges)
        {
            if (!nodeKeys.Contains(e.SourceNodeKey) || !nodeKeys.Contains(e.TargetNodeKey))
                continue;

            var srcNode = nodeByKey[e.SourceNodeKey];
            var tgtNode = nodeByKey[e.TargetNodeKey];
            var srcPorts = ResolvePorts(srcNode.NodeTypeKey, dbTypeMap);
            var tgtPorts = ResolvePorts(tgtNode.NodeTypeKey, dbTypeMap);

            if (srcPorts is null)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "UNKNOWN_NODE_TYPE",
                    Message = $"未解析到节点类型 '{srcNode.NodeTypeKey}' 的端口定义",
                    NodeKey = e.SourceNodeKey,
                });
                continue;
            }

            if (tgtPorts is null)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "UNKNOWN_NODE_TYPE",
                    Message = $"未解析到节点类型 '{tgtNode.NodeTypeKey}' 的端口定义",
                    NodeKey = e.TargetNodeKey,
                });
                continue;
            }

            var outPort = srcPorts.FirstOrDefault(p => p.PortKey == e.SourcePortKey && p.Direction == PortDirection.Output);
            var inPort = tgtPorts.FirstOrDefault(p => p.PortKey == e.TargetPortKey && p.Direction == PortDirection.Input);

            if (outPort is null)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "UNKNOWN_SOURCE_PORT",
                    Message = $"源节点 '{e.SourceNodeKey}' 上不存在输出端口 '{e.SourcePortKey}'",
                    NodeKey = e.SourceNodeKey,
                });
                continue;
            }

            if (inPort is null)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "UNKNOWN_TARGET_PORT",
                    Message = $"目标节点 '{e.TargetNodeKey}' 上不存在输入端口 '{e.TargetPortKey}'",
                    NodeKey = e.TargetNodeKey,
                });
                continue;
            }

            if (outPort.PortType != inPort.PortType)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "PORT_TYPE_MISMATCH",
                    Message = $"边 {e.SourceNodeKey}.{e.SourcePortKey} -> {e.TargetNodeKey}.{e.TargetPortKey} 的端口类型不兼容",
                    NodeKey = e.TargetNodeKey,
                });
                continue;
            }

            if (outPort.PortType == PortType.Data && !DataTypesCompatible(outPort.DataType, inPort.DataType))
            {
                errors.Add(new FlowValidationError
                {
                    Code = "DATA_TYPE_MISMATCH",
                    Message = $"数据边 {e.SourceNodeKey}.{e.SourcePortKey} -> {e.TargetNodeKey}.{e.TargetPortKey} 的数据类型不兼容",
                    NodeKey = e.TargetNodeKey,
                });
            }
        }

        return Finish(errors);
    }

    private IReadOnlyList<PortDefinition>? ResolvePorts(string typeKey, IReadOnlyDictionary<string, NodeTypeDefinition> dbTypeMap)
    {
        var decl = _nodeTypeRegistry.GetDeclaration(typeKey);
        if (decl is not null)
            return decl.GetPortDefinitions();

        if (!dbTypeMap.TryGetValue(typeKey, out var def))
            return null;
        return def.Ports ?? [];
    }

    private static bool DataTypesCompatible(NodeDataType src, NodeDataType dst)
    {
        if (src == dst)
            return true;
        if (src == NodeDataType.Any || dst == NodeDataType.Any)
            return true;
        if ((src == NodeDataType.Json && dst == NodeDataType.String) ||
            (src == NodeDataType.String && dst == NodeDataType.Json))
            return true;
        return false;
    }

    private static bool DfsCycle(
        string u,
        Dictionary<string, List<string>> forward,
        Dictionary<string, int> color,
        List<FlowValidationError> errors)
    {
        color[u] = 1;
        foreach (var v in forward[u])
        {
            if (color[v] == 1)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "CYCLE",
                    Message = $"检测到环路，涉及节点 '{v}'",
                    NodeKey = v,
                });
                return true;
            }

            if (color[v] == 0 && DfsCycle(v, forward, color, errors))
                return true;
        }

        color[u] = 2;
        return false;
    }

    private static FlowValidationResult Finish(List<FlowValidationError> errors)
    {
        return new FlowValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
        };
    }
}
