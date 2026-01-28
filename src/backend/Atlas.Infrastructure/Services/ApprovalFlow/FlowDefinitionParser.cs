using System.Text.Json;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 流程定义解析器（解析 DefinitionJson）
/// </summary>
public sealed class FlowDefinitionParser
{
    /// <summary>
    /// 解析流程定义 JSON，返回节点和边的结构化数据
    /// </summary>
    public static FlowDefinition Parse(string definitionJson)
    {
        using var doc = JsonDocument.Parse(definitionJson);
        var root = doc.RootElement;

        var nodes = new List<FlowNode>();
        var edges = new List<FlowEdge>();

        // 解析节点
        if (root.TryGetProperty("nodes", out var nodesElement) && nodesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var nodeElement in nodesElement.EnumerateArray())
            {
                var node = ParseNode(nodeElement);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }
        }

        // 解析边
        if (root.TryGetProperty("edges", out var edgesElement) && edgesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var edgeElement in edgesElement.EnumerateArray())
            {
                var edge = ParseEdge(edgeElement);
                if (edge != null)
                {
                    edges.Add(edge);
                }
            }
        }

        return new FlowDefinition(nodes, edges);
    }

    private static FlowNode? ParseNode(JsonElement nodeElement)
    {
        if (!nodeElement.TryGetProperty("id", out var idProp) || !nodeElement.TryGetProperty("type", out var typeProp))
        {
            return null;
        }

        var nodeId = idProp.GetString();
        var nodeType = typeProp.GetString();

        if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(nodeType))
        {
            return null;
        }

        var node = new FlowNode
        {
            Id = nodeId,
            Type = nodeType
        };

        // 解析 data 属性
        if (nodeElement.TryGetProperty("data", out var dataElement))
        {
            if (dataElement.TryGetProperty("label", out var labelProp))
            {
                node.Label = labelProp.GetString();
            }

            if (dataElement.TryGetProperty("assigneeType", out var assigneeTypeProp))
            {
                var assigneeTypeStr = assigneeTypeProp.GetString();
                if (Enum.TryParse<AssigneeType>(assigneeTypeStr, out var assigneeType))
                {
                    node.AssigneeType = assigneeType;
                }
            }

            if (dataElement.TryGetProperty("assigneeValue", out var assigneeValueProp))
            {
                node.AssigneeValue = assigneeValueProp.GetString();
            }

            if (dataElement.TryGetProperty("approvalMode", out var approvalModeProp))
            {
                var approvalModeStr = approvalModeProp.GetString();
                if (Enum.TryParse<ApprovalMode>(approvalModeStr, out var approvalMode))
                {
                    node.ApprovalMode = approvalMode;
                }
            }

            if (dataElement.TryGetProperty("conditionRule", out var conditionRuleProp))
            {
                node.ConditionRule = conditionRuleProp.GetRawText();
            }
        }

        return node;
    }

    private static FlowEdge? ParseEdge(JsonElement edgeElement)
    {
        if (!edgeElement.TryGetProperty("source", out var sourceProp) || !edgeElement.TryGetProperty("target", out var targetProp))
        {
            return null;
        }

        var source = sourceProp.GetString();
        var target = targetProp.GetString();

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            return null;
        }

        var edge = new FlowEdge
        {
            Source = source,
            Target = target
        };

        // 解析边的条件规则
        if (edgeElement.TryGetProperty("data", out var dataElement))
        {
            if (dataElement.TryGetProperty("conditionRule", out var conditionRuleProp))
            {
                edge.ConditionRule = conditionRuleProp.GetRawText();
            }
        }

        return edge;
    }
}

/// <summary>
/// 流程定义（节点和边的集合）
/// </summary>
public sealed class FlowDefinition
{
    public FlowDefinition(IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    public IReadOnlyList<FlowNode> Nodes { get; }
    public IReadOnlyList<FlowEdge> Edges { get; }

    /// <summary>
    /// 获取开始节点
    /// </summary>
    public FlowNode? GetStartNode()
    {
        return Nodes.FirstOrDefault(n => n.Type == "start");
    }

    /// <summary>
    /// 获取指定节点的所有出边
    /// </summary>
    public IReadOnlyList<FlowEdge> GetOutgoingEdges(string nodeId)
    {
        return Edges.Where(e => e.Source == nodeId).ToList();
    }

    /// <summary>
    /// 获取指定节点的所有入边
    /// </summary>
    public IReadOnlyList<FlowEdge> GetIncomingEdges(string nodeId)
    {
        return Edges.Where(e => e.Target == nodeId).ToList();
    }

    /// <summary>
    /// 根据 ID 获取节点
    /// </summary>
    public FlowNode? GetNodeById(string nodeId)
    {
        return Nodes.FirstOrDefault(n => n.Id == nodeId);
    }

    /// <summary>
    /// 判断节点是否为并行网关的汇聚节点（有多个入边）
    /// </summary>
    public bool IsParallelJoinGateway(string nodeId)
    {
        var node = GetNodeById(nodeId);
        if (node == null || node.Type != "parallelGateway")
        {
            return false;
        }

        var incomingEdges = GetIncomingEdges(nodeId);
        return incomingEdges.Count > 1;
    }

    /// <summary>
    /// 判断节点是否为并行网关的分支节点（有多个出边）
    /// </summary>
    public bool IsParallelSplitGateway(string nodeId)
    {
        var node = GetNodeById(nodeId);
        if (node == null || node.Type != "parallelGateway")
        {
            return false;
        }

        var outgoingEdges = GetOutgoingEdges(nodeId);
        return outgoingEdges.Count > 1;
    }
}

/// <summary>
/// 流程节点
/// </summary>
public sealed class FlowNode
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Label { get; set; }
    public AssigneeType AssigneeType { get; set; } = AssigneeType.User;
    public string? AssigneeValue { get; set; }
    public ApprovalMode ApprovalMode { get; set; } = ApprovalMode.All;
    public string? ConditionRule { get; set; }
}

/// <summary>
/// 流程边（连线）
/// </summary>
public sealed class FlowEdge
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? ConditionRule { get; set; }
}
