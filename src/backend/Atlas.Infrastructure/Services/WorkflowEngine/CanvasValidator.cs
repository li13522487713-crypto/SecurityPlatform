using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

public sealed class CanvasValidator : ICanvasValidator
{
    private static readonly IReadOnlyList<PortDefinition> DefaultPorts = new[]
    {
        BuildPort("input", PortDirection.Input),
        BuildPort("output", PortDirection.Output),
    };

    private static readonly Dictionary<WorkflowNodeType, IReadOnlyList<PortDefinition>> PortCatalog = new()
    {
        [WorkflowNodeType.Entry] = new[] { BuildPort("output", PortDirection.Output) },
        [WorkflowNodeType.Exit] = new[] { BuildPort("input", PortDirection.Input) },
        [WorkflowNodeType.Selector] = new[]
        {
            BuildPort("input", PortDirection.Input),
            BuildPort("true", PortDirection.Output),
            BuildPort("false", PortDirection.Output),
            BuildPort("output", PortDirection.Output),
        },
        [WorkflowNodeType.Loop] = new[]
        {
            BuildPort("input", PortDirection.Input),
            BuildPort("continue", PortDirection.Output),
            BuildPort("body", PortDirection.Output),
            BuildPort("loop_body", PortDirection.Output),
            BuildPort("done", PortDirection.Output),
            BuildPort("exit", PortDirection.Output),
            BuildPort("completed", PortDirection.Output),
            BuildPort("output", PortDirection.Output),
        },
        [WorkflowNodeType.OutputEmitter] = new[] { BuildPort("input", PortDirection.Input) },
    };

    public CanvasValidationResult ValidateCanvas(string canvasJson)
    {
        var errors = new List<CanvasValidationIssue>();

        var canvas = DagExecutor.ParseCanvas(canvasJson);
        if (canvas is null)
        {
            errors.Add(new CanvasValidationIssue("CANVAS_PARSE_FAILED", "画布 JSON 无法解析。"));
            return new CanvasValidationResult(false, errors);
        }

        var nodes = canvas.Nodes;
        if (nodes.Count == 0)
        {
            errors.Add(new CanvasValidationIssue("NO_NODES", "流程未包含任何节点。"));
            return new CanvasValidationResult(false, errors);
        }

        var nodeByKey = new Dictionary<string, NodeSchema>(StringComparer.OrdinalIgnoreCase);
        var nodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Key))
            {
                errors.Add(new CanvasValidationIssue("INVALID_NODE_KEY", "检测到空节点标识。"));
                continue;
            }

            if (!nodeByKey.TryAdd(node.Key, node))
            {
                errors.Add(new CanvasValidationIssue("DUPLICATE_NODE_KEY", $"重复节点标识 '{node.Key}'。", node.Key));
                continue;
            }

            if (!Enum.IsDefined(typeof(WorkflowNodeType), node.Type))
            {
                errors.Add(new CanvasValidationIssue("UNKNOWN_NODE_TYPE", $"节点 '{node.Key}' 的类型 '{node.Type}' 未找到端口定义。", node.Key));
            }

            nodeKeys.Add(node.Key);
        }

        if (nodeKeys.Count == 0)
        {
            errors.Add(new CanvasValidationIssue("NO_VALID_NODES", "未检测到可校验节点。"));
            return new CanvasValidationResult(false, errors);
        }

        var adjacency = nodeKeys.ToDictionary(key => key, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var inDegree = nodeKeys.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);
        var sourcePortUsed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var targetPortUsed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in canvas.Connections ?? Enumerable.Empty<ConnectionSchema>())
        {
            if (string.IsNullOrWhiteSpace(edge.SourceNodeKey))
            {
                errors.Add(new CanvasValidationIssue("EDGE_MISSING_SOURCE", "检测到未填写源节点的连线。"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(edge.TargetNodeKey))
            {
                errors.Add(new CanvasValidationIssue("EDGE_MISSING_TARGET", $"检测到源节点 '{edge.SourceNodeKey}' 的连线未填写目标节点。", edge.SourceNodeKey));
                continue;
            }

            if (!nodeByKey.ContainsKey(edge.SourceNodeKey))
            {
                errors.Add(new CanvasValidationIssue("EDGE_UNKNOWN_SOURCE", $"边引用了不存在的源节点 '{edge.SourceNodeKey}'。", edge.SourceNodeKey));
                continue;
            }

            if (!nodeByKey.ContainsKey(edge.TargetNodeKey))
            {
                errors.Add(new CanvasValidationIssue("EDGE_UNKNOWN_TARGET", $"边引用了不存在的目标节点 '{edge.TargetNodeKey}'。", edge.TargetNodeKey));
                continue;
            }

            if (string.IsNullOrWhiteSpace(edge.SourcePort))
            {
                errors.Add(new CanvasValidationIssue("EDGE_MISSING_SOURCE_PORT", $"源节点 '{edge.SourceNodeKey}' 的连线缺少源端口。", edge.SourceNodeKey));
                continue;
            }

            if (string.IsNullOrWhiteSpace(edge.TargetPort))
            {
                errors.Add(new CanvasValidationIssue("EDGE_MISSING_TARGET_PORT", $"目标节点 '{edge.TargetNodeKey}' 的连线缺少目标端口。", edge.TargetNodeKey));
                continue;
            }

            var sourceNode = nodeByKey[edge.SourceNodeKey];
            var targetNode = nodeByKey[edge.TargetNodeKey];
            var sourcePorts = ResolvePorts(sourceNode.Type);
            var targetPorts = ResolvePorts(targetNode.Type);

            var sourceOutputPort = sourcePorts.FirstOrDefault(
                x => string.Equals(x.PortKey, edge.SourcePort, StringComparison.OrdinalIgnoreCase) &&
                     x.Direction == PortDirection.Output);
            if (sourceOutputPort is null)
            {
                errors.Add(new CanvasValidationIssue(
                    "UNKNOWN_SOURCE_PORT",
                    $"源节点 '{edge.SourceNodeKey}' 上不存在输出端口 '{edge.SourcePort}'。",
                    edge.SourceNodeKey));
            }

            var targetInputPort = targetPorts.FirstOrDefault(
                x => string.Equals(x.PortKey, edge.TargetPort, StringComparison.OrdinalIgnoreCase) &&
                     x.Direction == PortDirection.Input);
            if (targetInputPort is null)
            {
                errors.Add(new CanvasValidationIssue(
                    "UNKNOWN_TARGET_PORT",
                    $"目标节点 '{edge.TargetNodeKey}' 上不存在输入端口 '{edge.TargetPort}'。",
                    edge.TargetNodeKey));
            }

            if (sourceOutputPort is null || targetInputPort is null)
            {
                continue;
            }

            if (sourceOutputPort.PortType != targetInputPort.PortType)
            {
                errors.Add(new CanvasValidationIssue(
                    "PORT_TYPE_MISMATCH",
                    $"连线端口类型不匹配：{edge.SourceNodeKey}.{edge.SourcePort} -> {edge.TargetNodeKey}.{edge.TargetPort}。",
                    edge.TargetNodeKey));
                continue;
            }

            if (sourceOutputPort.PortType == PortType.Data &&
                !DataTypesCompatible(sourceOutputPort.DataType, targetInputPort.DataType))
            {
                errors.Add(new CanvasValidationIssue(
                    "DATA_TYPE_MISMATCH",
                    $"连线数据类型不兼容：{edge.SourceNodeKey}.{edge.SourcePort} -> {edge.TargetNodeKey}.{edge.TargetPort}。",
                    edge.TargetNodeKey));
            }

            CheckPortMaxConnections(
                edge.SourceNodeKey,
                edge.SourcePort,
                sourceOutputPort.MaxConnections,
                sourcePortUsed,
                "SOURCE_PORT_MAX_CONNECTIONS",
                errors);

            CheckPortMaxConnections(
                edge.TargetNodeKey,
                edge.TargetPort,
                targetInputPort.MaxConnections,
                targetPortUsed,
                "TARGET_PORT_MAX_CONNECTIONS",
                errors);

            adjacency[edge.SourceNodeKey].Add(edge.TargetNodeKey);
            inDegree[edge.TargetNodeKey]++;
        }

        var entryNodeKeys = nodeKeys
            .Where(x => nodeByKey[x].Type == WorkflowNodeType.Entry)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (entryNodeKeys.Count == 0)
        {
            entryNodeKeys = inDegree
                .Where(x => x.Value == 0)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (entryNodeKeys.Count == 0)
        {
            errors.Add(new CanvasValidationIssue("NO_ENTRY_NODE", "流程未包含入口节点（无开始节点且不存在零入度节点）。"));
        }
        else
        {
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(entryNodeKeys);
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                if (!reachable.Add(u))
                {
                    continue;
                }

                foreach (var v in adjacency[u])
                {
                    if (!reachable.Contains(v))
                    {
                        queue.Enqueue(v);
                    }
                }
            }

            foreach (var nodeKey in nodeKeys)
            {
                if (!reachable.Contains(nodeKey))
                {
                    errors.Add(new CanvasValidationIssue(
                        "UNREACHABLE_NODE",
                        $"节点 '{nodeKey}' 无法从入口到达。",
                        nodeKey));
                }
            }
        }

        var color = nodeKeys.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var nodeKey in nodeKeys)
        {
            if (color[nodeKey] != 0)
            {
                continue;
            }

            DfsDetectCycle(nodeKey, adjacency, color, errors);
        }

        return new CanvasValidationResult(errors.Count == 0, errors);
    }

    private static IReadOnlyList<PortDefinition> ResolvePorts(WorkflowNodeType type)
    {
        return PortCatalog.TryGetValue(type, out var ports)
            ? ports
            : DefaultPorts;
    }

    private static void CheckPortMaxConnections(
        string nodeKey,
        string portKey,
        int maxConnections,
        Dictionary<string, int> usage,
        string code,
        List<CanvasValidationIssue> errors)
    {
        if (maxConnections <= 0)
        {
            return;
        }

        var usageKey = GetPortUsageKey(nodeKey, portKey);
        usage.TryGetValue(usageKey, out var count);
        count++;
        usage[usageKey] = count;
        if (count > maxConnections)
        {
            errors.Add(new CanvasValidationIssue(
                code,
                $"端口超出最大连接数({maxConnections})：{nodeKey}.{portKey}。",
                NodeKey: nodeKey));
        }
    }

    private static bool DfsDetectCycle(
        string nodeKey,
        Dictionary<string, List<string>> forward,
        Dictionary<string, int> color,
        List<CanvasValidationIssue> errors)
    {
        color[nodeKey] = 1;
        foreach (var target in forward[nodeKey])
        {
            if (!color.TryGetValue(target, out var targetColor))
            {
                continue;
            }

            if (targetColor == 1)
            {
                errors.Add(new CanvasValidationIssue(
                    "CYCLE_DETECTED",
                    $"检测到回环：涉及节点 '{target}'。",
                    target));
                return true;
            }

            if (targetColor == 0 && DfsDetectCycle(target, forward, color, errors))
            {
                return true;
            }
        }

        color[nodeKey] = 2;
        return false;
    }

    private static bool DataTypesCompatible(NodeDataType sourceDataType, NodeDataType targetDataType)
    {
        if (sourceDataType == targetDataType)
        {
            return true;
        }

        if (sourceDataType == NodeDataType.Any || targetDataType == NodeDataType.Any)
        {
            return true;
        }

        return (sourceDataType == NodeDataType.Json && targetDataType == NodeDataType.String) ||
               (sourceDataType == NodeDataType.String && targetDataType == NodeDataType.Json);
    }

    private static string GetPortUsageKey(string nodeKey, string portKey)
    {
        return $"{nodeKey}::{portKey}";
    }

    private static PortDefinition BuildPort(string portKey, PortDirection direction)
    {
        return new PortDefinition
        {
            PortKey = portKey,
            DisplayName = portKey,
            Direction = direction,
            PortType = PortType.Data,
            DataType = NodeDataType.Any,
            IsRequired = false,
            MaxConnections = 1,
        };
    }
}

