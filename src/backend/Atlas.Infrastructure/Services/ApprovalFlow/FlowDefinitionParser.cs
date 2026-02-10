using System.Text.Json;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 流程定义解析器（解析 DefinitionJson）
/// </summary>
public sealed class FlowDefinitionParser
{
    /// <summary>
    /// 解析流程定义 JSON，返回节点和边的结构化数据。
    /// 支持两种格式：
    /// 1. nodes[] + edges[] 数组格式（图结构）
    /// 2. nodes.rootNode 树格式（前端设计器输出）
    /// </summary>
    public static FlowDefinition Parse(string definitionJson)
    {
        using var doc = JsonDocument.Parse(definitionJson);
        var root = doc.RootElement;

        // 检测是否为前端设计器输出的 tree 格式
        if (TreeToGraphConverter.IsTreeFormat(root))
        {
            var (treeNodes, treeEdges) = TreeToGraphConverter.Convert(root);
            return new FlowDefinition(treeNodes, treeEdges);
        }

        var nodes = new List<FlowNode>();
        var edges = new List<FlowEdge>();

        // 解析节点（数组格式）
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

        // 解析边（数组格式）
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

            if (dataElement.TryGetProperty("missingAssigneeStrategy", out var missingStrategyProp))
            {
                var missingStrategyStr = missingStrategyProp.GetString();
                if (Enum.TryParse<MissingAssigneeStrategy>(missingStrategyStr, out var missingStrategy))
                {
                    node.MissingAssigneeStrategy = missingStrategy;
                }
            }

            if (dataElement.TryGetProperty("deduplicationType", out var deduplicationTypeProp))
            {
                var deduplicationTypeStr = deduplicationTypeProp.GetString();
                if (Enum.TryParse<DeduplicationType>(deduplicationTypeStr, out var deduplicationType))
                {
                    node.DeduplicationType = deduplicationType;
                }
            }

            if (dataElement.TryGetProperty("excludeUserIds", out var excludeUserIdsProp))
            {
                node.ExcludeUserIds = excludeUserIdsProp.GetRawText();
            }

            if (dataElement.TryGetProperty("excludeRoleCodes", out var excludeRoleCodesProp))
            {
                node.ExcludeRoleCodes = excludeRoleCodesProp.GetRawText();
            }

            // 解析超时配置
            if (dataElement.TryGetProperty("timeoutEnabled", out var timeoutEnabledProp))
            {
                node.TimeoutEnabled = timeoutEnabledProp.GetBoolean();
            }

            if (dataElement.TryGetProperty("timeoutHours", out var timeoutHoursProp))
            {
                if (timeoutHoursProp.ValueKind == JsonValueKind.Number)
                {
                    node.TimeoutHours = timeoutHoursProp.GetInt32();
                }
            }

            if (dataElement.TryGetProperty("timeoutMinutes", out var timeoutMinutesProp))
            {
                if (timeoutMinutesProp.ValueKind == JsonValueKind.Number)
                {
                    node.TimeoutMinutes = timeoutMinutesProp.GetInt32();
                }
            }

            if (dataElement.TryGetProperty("reminderIntervalHours", out var reminderIntervalProp))
            {
                if (reminderIntervalProp.ValueKind == JsonValueKind.Number)
                {
                    node.ReminderIntervalHours = reminderIntervalProp.GetInt32();
                }
            }

            if (dataElement.TryGetProperty("maxReminderCount", out var maxReminderCountProp))
            {
                if (maxReminderCountProp.ValueKind == JsonValueKind.Number)
                {
                    node.MaxReminderCount = maxReminderCountProp.GetInt32();
                }
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
    public MissingAssigneeStrategy MissingAssigneeStrategy { get; set; } = MissingAssigneeStrategy.NotAllowed;
    public DeduplicationType DeduplicationType { get; set; } = DeduplicationType.None;
    public string? ExcludeUserIds { get; set; } // 排除的用户ID列表（逗号分隔或JSON数组）
    public string? ExcludeRoleCodes { get; set; } // 排除的角色代码列表（逗号分隔或JSON数组）
    
    // 超时配置
    public bool TimeoutEnabled { get; set; } = false;
    public int? TimeoutHours { get; set; }
    public int? TimeoutMinutes { get; set; }
    public int? ReminderIntervalHours { get; set; }
    public int? MaxReminderCount { get; set; }

    /// <summary>
    /// 超时自动处理动作（None = 仅提醒，AutoApprove = 自动通过，AutoReject = 自动驳回，AutoSkip = 自动跳过）
    /// </summary>
    public TimeoutAction TimeoutAction { get; set; } = TimeoutAction.None;
}

/// <summary>
/// 节点超时自动处理动作
/// </summary>
public enum TimeoutAction
{
    /// <summary>仅提醒，不自动处理</summary>
    None = 0,

    /// <summary>超时自动通过</summary>
    AutoApprove = 1,

    /// <summary>超时自动驳回</summary>
    AutoReject = 2,

    /// <summary>超时自动跳过（跳到下一个节点）</summary>
    AutoSkip = 3
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
