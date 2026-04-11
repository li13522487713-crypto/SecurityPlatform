using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Domain.LogicFlow.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

public sealed class CanvasValidator : ICanvasValidator
{
    private static readonly Regex VariablePlaceholderRegex = new(@"\{\{\s*(?<path>[^{}]+?)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex VariablePathRegex = new(@"^[A-Za-z0-9_-]+(\[[0-9]+\])?(\.[A-Za-z0-9_-]+(\[[0-9]+\])?)*$", RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedVariableRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "input",
        "inputs",
        "global",
        "globals",
        "vars",
        "variables",
        "env",
        "context",
        "runtime",
        "system",
        "workflow",
        "user",
        "tenant"
    };

    private static readonly IReadOnlyList<PortDefinition> DefaultPorts = new[]
    {
        BuildPort("input", PortDirection.Input),
        BuildPort("output", PortDirection.Output),
    };

    private static readonly Dictionary<WorkflowNodeType, IReadOnlyList<PortDefinition>> LegacyCompatibleExtraPorts = new()
    {
        [WorkflowNodeType.Selector] = new[]
        {
            BuildPort("output", PortDirection.Output),
        },
        [WorkflowNodeType.Loop] = new[]
        {
            BuildPort("loop_body", PortDirection.Output),
            BuildPort("exit", PortDirection.Output),
            BuildPort("completed", PortDirection.Output),
            BuildPort("output", PortDirection.Output),
        },
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
        var outgoingConnectionsByNode = new Dictionary<string, List<ConnectionSchema>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodeByKey.Values)
        {
            ValidateNodePortSchemas(node, errors);
        }

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

            if (!outgoingConnectionsByNode.TryGetValue(edge.SourceNodeKey, out var outgoingConnections))
            {
                outgoingConnections = new List<ConnectionSchema>();
                outgoingConnectionsByNode[edge.SourceNodeKey] = outgoingConnections;
            }

            outgoingConnections.Add(edge);

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
            var sourcePorts = ResolvePorts(sourceNode);
            var targetPorts = ResolvePorts(targetNode);

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

        ValidateRequiredInputPortBindings(nodeByKey, targetPortUsed, errors);
        ValidateBranchConfigurations(nodeByKey, outgoingConnectionsByNode, errors);
        ValidateVariableMappings(
            nodeByKey,
            adjacency,
            canvas.Globals?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            errors);

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

    private static IReadOnlyList<PortDefinition> ResolvePorts(NodeSchema node)
    {
        if (node.Ports is { Count: > 0 })
        {
            var customPorts = new List<PortDefinition>(node.Ports.Count);
            foreach (var customPort in node.Ports)
            {
                if (!TryParsePortDirection(customPort.Direction, out var direction) ||
                    string.IsNullOrWhiteSpace(customPort.Key))
                {
                    continue;
                }

                customPorts.Add(new PortDefinition
                {
                    PortKey = customPort.Key.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(customPort.Name) ? customPort.Key.Trim() : customPort.Name.Trim(),
                    Direction = direction,
                    PortType = PortType.Data,
                    DataType = ParseNodeDataType(customPort.DataType),
                    IsRequired = customPort.IsRequired ?? false,
                    MaxConnections = customPort.MaxConnections.GetValueOrDefault(1) <= 0
                        ? 1
                        : customPort.MaxConnections.GetValueOrDefault(1)
                });
            }

            if (customPorts.Count > 0)
            {
                return customPorts;
            }
        }

        return ResolvePorts(node.Type);
    }

    private static IReadOnlyList<PortDefinition> ResolvePorts(WorkflowNodeType type)
    {
        var declarationPorts = BuiltInWorkflowNodeDeclarations.GetPorts(type)
            .Select(ToPortDefinition)
            .ToList();
        if (declarationPorts.Count == 0)
        {
            declarationPorts.AddRange(DefaultPorts);
        }

        if (LegacyCompatibleExtraPorts.TryGetValue(type, out var extraPorts))
        {
            foreach (var extraPort in extraPorts)
            {
                if (!declarationPorts.Any(x =>
                        string.Equals(x.PortKey, extraPort.PortKey, StringComparison.OrdinalIgnoreCase) &&
                        x.Direction == extraPort.Direction))
                {
                    declarationPorts.Add(extraPort);
                }
            }
        }

        return declarationPorts;
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

    private static void ValidateRequiredInputPortBindings(
        IReadOnlyDictionary<string, NodeSchema> nodeByKey,
        IReadOnlyDictionary<string, int> targetPortUsed,
        List<CanvasValidationIssue> errors)
    {
        foreach (var node in nodeByKey.Values)
        {
            var inputPorts = ResolvePorts(node)
                .Where(x => x.Direction == PortDirection.Input && x.IsRequired)
                .ToArray();

            foreach (var inputPort in inputPorts)
            {
                var usageKey = GetPortUsageKey(node.Key, inputPort.PortKey);
                targetPortUsed.TryGetValue(usageKey, out var count);
                if (count > 0)
                {
                    continue;
                }

                errors.Add(new CanvasValidationIssue(
                    "REQUIRED_INPUT_PORT_UNBOUND",
                    $"节点 '{node.Key}' 的必填输入端口 '{inputPort.PortKey}' 未被连接。",
                    node.Key,
                    TargetPort: inputPort.PortKey));
            }
        }
    }

    private static void ValidateNodePortSchemas(
        NodeSchema node,
        List<CanvasValidationIssue> errors)
    {
        if (node.Ports is null)
        {
            return;
        }

        if (node.Ports.Count == 0)
        {
            errors.Add(new CanvasValidationIssue(
                "PORT_SCHEMA_EMPTY",
                $"节点 '{node.Key}' 提供了空 ports 定义，至少需要 1 个端口。",
                node.Key));
            return;
        }

        var seenPortKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < node.Ports.Count; i++)
        {
            var port = node.Ports[i];
            var indexLabel = $"ports[{i}]";

            if (string.IsNullOrWhiteSpace(port.Key))
            {
                errors.Add(new CanvasValidationIssue(
                    "PORT_SCHEMA_KEY_REQUIRED",
                    $"节点 '{node.Key}' 的 {indexLabel} 缺少端口 key。",
                    node.Key));
                continue;
            }

            if (string.IsNullOrWhiteSpace(port.Name))
            {
                errors.Add(new CanvasValidationIssue(
                    "PORT_SCHEMA_NAME_REQUIRED",
                    $"节点 '{node.Key}' 的端口 '{port.Key}' 缺少名称。",
                    node.Key,
                    TargetPort: port.Key));
            }

            if (!TryParsePortDirection(port.Direction, out _))
            {
                errors.Add(new CanvasValidationIssue(
                    "PORT_SCHEMA_DIRECTION_INVALID",
                    $"节点 '{node.Key}' 的端口 '{port.Key}' direction='{port.Direction}' 非法，仅支持 input/output。",
                    node.Key,
                    TargetPort: port.Key));
            }

            var dedupeKey = $"{port.Direction}:{port.Key}".Trim();
            if (!seenPortKeys.Add(dedupeKey))
            {
                errors.Add(new CanvasValidationIssue(
                    "PORT_SCHEMA_DUPLICATE",
                    $"节点 '{node.Key}' 存在重复端口定义 '{dedupeKey}'。",
                    node.Key,
                    TargetPort: port.Key));
            }

            if (port.MaxConnections is <= 0)
            {
                errors.Add(new CanvasValidationIssue(
                    "PORT_SCHEMA_MAX_CONNECTIONS_INVALID",
                    $"节点 '{node.Key}' 的端口 '{port.Key}' MaxConnections 必须大于 0。",
                    node.Key,
                    TargetPort: port.Key));
            }

            if (!string.IsNullOrWhiteSpace(port.DataType) && !IsSupportedDataType(port.DataType))
            {
                errors.Add(new CanvasValidationIssue(
                    "PORT_SCHEMA_DATA_TYPE_INVALID",
                    $"节点 '{node.Key}' 的端口 '{port.Key}' DataType='{port.DataType}' 不受支持。",
                    node.Key,
                    TargetPort: port.Key));
            }
        }
    }

    private static void ValidateBranchConfigurations(
        IReadOnlyDictionary<string, NodeSchema> nodeByKey,
        IReadOnlyDictionary<string, List<ConnectionSchema>> outgoingConnectionsByNode,
        List<CanvasValidationIssue> errors)
    {
        foreach (var node in nodeByKey.Values)
        {
            if (!outgoingConnectionsByNode.TryGetValue(node.Key, out var outgoingConnections))
            {
                outgoingConnections = [];
            }

            switch (node.Type)
            {
                case WorkflowNodeType.Selector:
                    ValidateSelectorBranchConfiguration(node, outgoingConnections, errors);
                    break;
                case WorkflowNodeType.Loop:
                    ValidateLoopBranchConfiguration(node, outgoingConnections, errors);
                    break;
            }
        }
    }

    private static void ValidateSelectorBranchConfiguration(
        NodeSchema node,
        IReadOnlyList<ConnectionSchema> outgoingConnections,
        List<CanvasValidationIssue> errors)
    {
        if (outgoingConnections.Count < 2)
        {
            errors.Add(new CanvasValidationIssue(
                "SELECTOR_BRANCH_COUNT_INVALID",
                $"Selector 节点 '{node.Key}' 至少需要 2 条分支连线。",
                node.Key));
        }

        var condition = VariableResolver.GetConfigString(node.Config, "condition");
        var hasStructuredConditions = node.Config.TryGetValue("conditions", out var rawConditions) &&
                                      rawConditions.ValueKind == JsonValueKind.Array &&
                                      rawConditions.GetArrayLength() > 0;
        if (string.IsNullOrWhiteSpace(condition) && !hasStructuredConditions)
        {
            errors.Add(new CanvasValidationIssue(
                "SELECTOR_CONDITION_MISSING",
                $"Selector 节点 '{node.Key}' 缺少 condition 或 conditions 配置。",
                node.Key));
        }

        var trueConnections = outgoingConnections.Where(IsSelectorTrueConnection).ToArray();
        var falseConnections = outgoingConnections.Where(IsSelectorFalseConnection).ToArray();
        var fallbackTwoBranches = trueConnections.Length == 0 &&
                                  falseConnections.Length == 0 &&
                                  outgoingConnections.Count == 2;

        if (!fallbackTwoBranches)
        {
            if (trueConnections.Length == 0)
            {
                errors.Add(new CanvasValidationIssue(
                    "SELECTOR_TRUE_BRANCH_MISSING",
                    $"Selector 节点 '{node.Key}' 未识别到 true 分支连线。",
                    node.Key));
            }

            if (falseConnections.Length == 0)
            {
                errors.Add(new CanvasValidationIssue(
                    "SELECTOR_FALSE_BRANCH_MISSING",
                    $"Selector 节点 '{node.Key}' 未识别到 false 分支连线。",
                    node.Key));
            }
        }
    }

    private static void ValidateLoopBranchConfiguration(
        NodeSchema node,
        IReadOnlyList<ConnectionSchema> outgoingConnections,
        List<CanvasValidationIssue> errors)
    {
        if (outgoingConnections.Count == 0)
        {
            errors.Add(new CanvasValidationIssue(
                "LOOP_BRANCH_MISSING",
                $"Loop 节点 '{node.Key}' 缺少任何下游分支。",
                node.Key));
            return;
        }

        var mode = VariableResolver.GetConfigString(node.Config, "mode", "count").Trim().ToLowerInvariant();
        if (mode == "while" && string.IsNullOrWhiteSpace(VariableResolver.GetConfigString(node.Config, "condition")))
        {
            errors.Add(new CanvasValidationIssue(
                "LOOP_CONDITION_MISSING",
                $"Loop 节点 '{node.Key}' 在 mode=while 时必须配置 condition。",
                node.Key));
        }

        if (mode == "foreach" && string.IsNullOrWhiteSpace(VariableResolver.GetConfigString(node.Config, "collectionPath")))
        {
            errors.Add(new CanvasValidationIssue(
                "LOOP_COLLECTION_PATH_MISSING",
                $"Loop 节点 '{node.Key}' 在 mode=forEach 时必须配置 collectionPath。",
                node.Key));
        }

        var continueConnections = outgoingConnections.Where(IsLoopContinueConnection).ToArray();
        var exitConnections = outgoingConnections.Where(IsLoopExitConnection).ToArray();
        var fallbackTwoBranches = continueConnections.Length == 0 &&
                                  exitConnections.Length == 0 &&
                                  outgoingConnections.Count >= 2;

        if (!fallbackTwoBranches && continueConnections.Length == 0)
        {
            errors.Add(new CanvasValidationIssue(
                "LOOP_CONTINUE_BRANCH_MISSING",
                $"Loop 节点 '{node.Key}' 未识别到循环体分支（continue/body）。",
                node.Key));
        }

        if (!fallbackTwoBranches && exitConnections.Length == 0)
        {
            errors.Add(new CanvasValidationIssue(
                "LOOP_EXIT_BRANCH_MISSING",
                $"Loop 节点 '{node.Key}' 未识别到退出分支（exit/completed）。",
                node.Key));
        }
    }

    private static void ValidateVariableMappings(
        IReadOnlyDictionary<string, NodeSchema> nodeByKey,
        IReadOnlyDictionary<string, List<string>> adjacency,
        IReadOnlySet<string> globalKeys,
        List<CanvasValidationIssue> errors)
    {
        var reverseAdjacency = BuildReverseAdjacency(nodeByKey.Keys, adjacency);
        foreach (var node in nodeByKey.Values)
        {
            var upstreamNodes = BuildUpstreamNodes(node.Key, reverseAdjacency);
            ValidateNodeFieldMappings(node, node.InputSources, isInputMapping: true, upstreamNodes, nodeByKey, globalKeys, errors);
            ValidateNodeFieldMappings(node, node.OutputSources, isInputMapping: false, upstreamNodes, nodeByKey, globalKeys, errors);
        }
    }

    private static void ValidateNodeFieldMappings(
        NodeSchema node,
        IReadOnlyList<NodeFieldMapping>? mappings,
        bool isInputMapping,
        IReadOnlySet<string> upstreamNodes,
        IReadOnlyDictionary<string, NodeSchema> nodeByKey,
        IReadOnlySet<string> globalKeys,
        List<CanvasValidationIssue> errors)
    {
        if (mappings is null)
        {
            return;
        }

        var mappingLabel = isInputMapping ? "inputSources" : "outputSources";
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < mappings.Count; i++)
        {
            var mapping = mappings[i];
            if (string.IsNullOrWhiteSpace(mapping.Field))
            {
                errors.Add(new CanvasValidationIssue(
                    "VARIABLE_MAPPING_FIELD_REQUIRED",
                    $"节点 '{node.Key}' 的 {mappingLabel}[{i}] 缺少 field。",
                    node.Key));
                continue;
            }

            if (!seenFields.Add(mapping.Field.Trim()))
            {
                errors.Add(new CanvasValidationIssue(
                    "VARIABLE_MAPPING_FIELD_DUPLICATE",
                    $"节点 '{node.Key}' 的 {mappingLabel} 中字段 '{mapping.Field}' 重复。",
                    node.Key));
            }

            if (string.IsNullOrWhiteSpace(mapping.Path))
            {
                errors.Add(new CanvasValidationIssue(
                    "VARIABLE_MAPPING_PATH_REQUIRED",
                    $"节点 '{node.Key}' 的 {mappingLabel}[{i}] 缺少 path。",
                    node.Key));
                continue;
            }

            var variablePaths = EnumerateVariablePaths(mapping.Path).ToArray();
            if (variablePaths.Length == 0)
            {
                errors.Add(new CanvasValidationIssue(
                    "VARIABLE_MAPPING_PATH_INVALID",
                    $"节点 '{node.Key}' 的 {mappingLabel}[{i}] path 不是合法变量路径。",
                    node.Key));
                continue;
            }

            foreach (var variablePath in variablePaths)
            {
                if (!VariablePathRegex.IsMatch(variablePath))
                {
                    errors.Add(new CanvasValidationIssue(
                        "VARIABLE_MAPPING_PATH_INVALID",
                        $"节点 '{node.Key}' 的 {mappingLabel}[{i}] path='{variablePath}' 非法。",
                        node.Key));
                    continue;
                }

                if (!isInputMapping)
                {
                    continue;
                }

                var root = GetVariableRoot(variablePath);
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                if (string.Equals(root, "global", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(root, "globals", StringComparison.OrdinalIgnoreCase))
                {
                    var globalKey = GetGlobalVariableKey(variablePath);
                    if (!string.IsNullOrWhiteSpace(globalKey) && globalKeys.Contains(globalKey))
                    {
                        continue;
                    }

                    errors.Add(new CanvasValidationIssue(
                        "VARIABLE_MAPPING_GLOBAL_MISSING",
                        $"节点 '{node.Key}' 的输入映射引用了不存在的全局变量 '{variablePath}'。",
                        node.Key));
                    continue;
                }

                if (ReservedVariableRoots.Contains(root))
                {
                    continue;
                }

                if (!nodeByKey.ContainsKey(root))
                {
                    continue;
                }

                if (upstreamNodes.Contains(root))
                {
                    continue;
                }

                errors.Add(new CanvasValidationIssue(
                    "VARIABLE_MAPPING_SCOPE_INVALID",
                    $"节点 '{node.Key}' 的输入映射引用了非上游节点变量 '{root}'。",
                    node.Key));
            }
        }
    }

    private static Dictionary<string, List<string>> BuildReverseAdjacency(
        IEnumerable<string> nodeKeys,
        IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var reverse = nodeKeys.ToDictionary(x => x, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (source, targets) in adjacency)
        {
            foreach (var target in targets)
            {
                if (!reverse.TryGetValue(target, out var sourceList))
                {
                    sourceList = new List<string>();
                    reverse[target] = sourceList;
                }

                sourceList.Add(source);
            }
        }

        return reverse;
    }

    private static HashSet<string> BuildUpstreamNodes(
        string nodeKey,
        IReadOnlyDictionary<string, List<string>> reverseAdjacency)
    {
        var upstream = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!reverseAdjacency.TryGetValue(nodeKey, out var parents))
        {
            return upstream;
        }

        var queue = new Queue<string>(parents);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!upstream.Add(current))
            {
                continue;
            }

            if (!reverseAdjacency.TryGetValue(current, out var grandParents))
            {
                continue;
            }

            foreach (var grandParent in grandParents)
            {
                queue.Enqueue(grandParent);
            }
        }

        return upstream;
    }

    private static IEnumerable<string> EnumerateVariablePaths(string mappingPath)
    {
        var path = mappingPath.Trim();
        if (path.Length == 0)
        {
            return [];
        }

        var matches = VariablePlaceholderRegex.Matches(path);
        if (matches.Count == 0)
        {
            return [path];
        }

        var paths = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            var candidate = match.Groups["path"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                paths.Add(candidate);
            }
        }

        return paths;
    }

    private static string GetVariableRoot(string variablePath)
    {
        var separatorIndex = variablePath.IndexOfAny(['.', '[']);
        return separatorIndex < 0
            ? variablePath.Trim()
            : variablePath[..separatorIndex].Trim();
    }

    private static string GetGlobalVariableKey(string variablePath)
    {
        var normalized = variablePath.Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var dotIndex = normalized.IndexOf('.');
        if (dotIndex < 0 || dotIndex == normalized.Length - 1)
        {
            return string.Empty;
        }

        var tail = normalized[(dotIndex + 1)..];
        var keyEnd = tail.IndexOfAny(['.', '[']);
        return keyEnd < 0
            ? tail.Trim()
            : tail[..keyEnd].Trim();
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

    private static PortDefinition ToPortDefinition(WorkflowNodePortMetadata metadata)
    {
        return new PortDefinition
        {
            PortKey = metadata.Key,
            DisplayName = metadata.Name,
            Direction = metadata.Direction == WorkflowNodePortDirection.Input
                ? PortDirection.Input
                : PortDirection.Output,
            PortType = PortType.Data,
            DataType = ParseNodeDataType(metadata.DataType),
            IsRequired = metadata.IsRequired,
            MaxConnections = metadata.MaxConnections <= 0 ? 1 : metadata.MaxConnections
        };
    }

    private static NodeDataType ParseNodeDataType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NodeDataType.Any;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "string" => NodeDataType.String,
            "number" or "int" or "float" or "double" or "decimal" => NodeDataType.Number,
            "boolean" or "bool" => NodeDataType.Boolean,
            "array" => NodeDataType.Array,
            "object" or "map" or "dict" => NodeDataType.Record,
            "json" => NodeDataType.Json,
            _ => NodeDataType.Any
        };
    }

    private static bool TryParsePortDirection(string? value, out PortDirection direction)
    {
        direction = PortDirection.Input;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "input", StringComparison.OrdinalIgnoreCase))
        {
            direction = PortDirection.Input;
            return true;
        }

        if (string.Equals(normalized, "output", StringComparison.OrdinalIgnoreCase))
        {
            direction = PortDirection.Output;
            return true;
        }

        return false;
    }

    private static bool IsSupportedDataType(string dataType)
    {
        return dataType.Trim().ToLowerInvariant() switch
        {
            "any" => true,
            "string" => true,
            "number" => true,
            "int" => true,
            "float" => true,
            "double" => true,
            "decimal" => true,
            "boolean" => true,
            "bool" => true,
            "array" => true,
            "object" => true,
            "record" => true,
            "map" => true,
            "dict" => true,
            "json" => true,
            _ => false
        };
    }

    private static bool IsSelectorTrueConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("selector_result", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Contains("selected_branch", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true_branch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("true", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("true", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("yes", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelectorFalseConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("selector_result", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Contains("selected_branch", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false_branch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("false", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("false", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("no", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("no", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopContinueConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("loop_completed", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("continue", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("continue", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("loop_body", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("loop_body", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("body", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("body", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopExitConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("loop_completed", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("done", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("done", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("completed", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("completed", StringComparison.OrdinalIgnoreCase);
    }
}

