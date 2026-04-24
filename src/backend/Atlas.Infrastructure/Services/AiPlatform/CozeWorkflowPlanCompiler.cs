using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// Coze 原生 schema 运行时编译器：
/// 输入 nodes[id/type/meta/data] + edges，输出 Atlas 内部可执行 CanvasSchema。
/// </summary>
public sealed class CozeWorkflowPlanCompiler : ICozeWorkflowPlanCompiler
{
    private static readonly IReadOnlyDictionary<string, WorkflowNodeType> CozeNodeTypeMap =
        new Dictionary<string, WorkflowNodeType>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = WorkflowNodeType.Entry,
            ["2"] = WorkflowNodeType.Exit,
            ["3"] = WorkflowNodeType.Llm,
            ["4"] = WorkflowNodeType.Plugin,
            ["5"] = WorkflowNodeType.CodeRunner,
            ["6"] = WorkflowNodeType.KnowledgeRetriever,
            ["8"] = WorkflowNodeType.Selector,
            ["9"] = WorkflowNodeType.SubWorkflow,
            ["11"] = WorkflowNodeType.Variable,
            // Coze 通用 Database 父节点（ID=12）：Atlas 已用 5 个细粒度 Database 节点替代。
            ["12"] = WorkflowNodeType.Comment,
            ["13"] = WorkflowNodeType.OutputEmitter,
            ["14"] = WorkflowNodeType.ImageGenerate,
            ["15"] = WorkflowNodeType.TextProcessor,
            ["16"] = WorkflowNodeType.ImageReference,
            ["17"] = WorkflowNodeType.ImageCanvas,
            ["18"] = WorkflowNodeType.QuestionAnswer,
            ["19"] = WorkflowNodeType.Break,
            ["20"] = WorkflowNodeType.VariableAssignerWithinLoop,
            ["21"] = WorkflowNodeType.Loop,
            ["22"] = WorkflowNodeType.IntentDetector,
            ["24"] = WorkflowNodeType.SceneVariable,
            ["25"] = WorkflowNodeType.SceneChat,
            ["26"] = WorkflowNodeType.LtmUpstream,
            ["27"] = WorkflowNodeType.KnowledgeIndexer,
            ["28"] = WorkflowNodeType.Batch,
            ["29"] = WorkflowNodeType.Continue,
            ["30"] = WorkflowNodeType.InputReceiver,
            ["31"] = WorkflowNodeType.Comment,
            ["32"] = WorkflowNodeType.VariableAggregator,
            ["34"] = WorkflowNodeType.TriggerUpsert,
            ["35"] = WorkflowNodeType.TriggerDelete,
            ["36"] = WorkflowNodeType.TriggerRead,
            ["37"] = WorkflowNodeType.MessageList,
            ["38"] = WorkflowNodeType.ClearConversationHistory,
            ["39"] = WorkflowNodeType.CreateConversation,
            ["40"] = WorkflowNodeType.AssignVariable,
            ["41"] = WorkflowNodeType.DatabaseCustomSql,
            ["42"] = WorkflowNodeType.DatabaseUpdate,
            ["43"] = WorkflowNodeType.DatabaseQuery,
            ["44"] = WorkflowNodeType.DatabaseDelete,
            ["45"] = WorkflowNodeType.HttpRequester,
            ["46"] = WorkflowNodeType.DatabaseInsert,
            ["51"] = WorkflowNodeType.ConversationUpdate,
            ["52"] = WorkflowNodeType.ConversationDelete,
            ["53"] = WorkflowNodeType.ConversationList,
            ["54"] = WorkflowNodeType.ConversationHistory,
            ["55"] = WorkflowNodeType.CreateMessage,
            ["56"] = WorkflowNodeType.EditMessage,
            ["57"] = WorkflowNodeType.DeleteMessage,
            ["58"] = WorkflowNodeType.JsonSerialization,
            ["59"] = WorkflowNodeType.JsonDeserialization,
            ["60"] = WorkflowNodeType.Agent,
            ["61"] = WorkflowNodeType.KnowledgeDeleter,
            ["62"] = WorkflowNodeType.Ltm,
            ["Start"] = WorkflowNodeType.Entry,
            ["Entry"] = WorkflowNodeType.Entry,
            ["End"] = WorkflowNodeType.Exit,
            ["Exit"] = WorkflowNodeType.Exit,
            ["Code"] = WorkflowNodeType.CodeRunner,
            ["CodeRunner"] = WorkflowNodeType.CodeRunner,
            ["If"] = WorkflowNodeType.Selector,
            ["Condition"] = WorkflowNodeType.Selector,
            ["Http"] = WorkflowNodeType.HttpRequester,
            ["HTTPRequest"] = WorkflowNodeType.HttpRequester,
            ["Database"] = WorkflowNodeType.Comment,
        };

    public CozeWorkflowCompileResult Compile(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return Fail("COZE_SCHEMA_EMPTY", "Coze 画布为空。");
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return TryCompile(document.RootElement, out var canvas, out var errors)
                ? new CozeWorkflowCompileResult(true, canvas, Array.Empty<CanvasValidationIssue>())
                : new CozeWorkflowCompileResult(false, null, errors);
        }
        catch (JsonException ex)
        {
            return Fail("COZE_SCHEMA_PARSE_FAILED", $"Coze 画布 JSON 无法解析：{ex.Message}");
        }
    }

    private static bool TryCompile(
        JsonElement root,
        out CanvasSchema? canvas,
        out IReadOnlyList<CanvasValidationIssue> errors)
    {
        canvas = null;
        var issues = new List<CanvasValidationIssue>();

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new CanvasValidationIssue("COZE_SCHEMA_ROOT_INVALID", "Coze 画布根节点必须为对象。"));
            errors = issues;
            return false;
        }

        if (!TryGetProperty(root, "nodes", out var nodesElement) || nodesElement.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new CanvasValidationIssue("COZE_NODES_MISSING", "Coze 画布缺少 nodes 数组。"));
            errors = issues;
            return false;
        }

        if (!TryGetProperty(root, "edges", out var edgesElement) || edgesElement.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new CanvasValidationIssue("COZE_EDGES_MISSING", "Coze 画布缺少 edges 数组。"));
            errors = issues;
            return false;
        }

        if (!TryCompileCanvas(
                nodesElement,
                edgesElement,
                out canvas,
                out var compileIssue))
        {
            issues.Add(compileIssue);
            errors = issues;
            return false;
        }

        errors = Array.Empty<CanvasValidationIssue>();
        return true;
    }

    private static bool TryCompileCanvas(
        JsonElement nodesElement,
        JsonElement edgesElement,
        out CanvasSchema? canvas,
        out CanvasValidationIssue issue)
    {
        canvas = null;
        issue = default!;

        if (nodesElement.ValueKind != JsonValueKind.Array || edgesElement.ValueKind != JsonValueKind.Array)
        {
            issue = new CanvasValidationIssue("COZE_CANVAS_INVALID", "Coze 子画布节点或连线不是数组。");
            return false;
        }

        var nodes = new List<NodeSchema>();
        foreach (var nodeElement in nodesElement.EnumerateArray())
        {
            if (!TryConvertNode(nodeElement, out var node, out issue))
            {
                return false;
            }

            nodes.Add(node);
        }

        var connections = new List<ConnectionSchema>();
        foreach (var edgeElement in edgesElement.EnumerateArray())
        {
            if (!TryConvertEdge(edgeElement, out var connection, out issue))
            {
                return false;
            }

            connections.Add(connection);
        }

        canvas = new CanvasSchema(nodes, connections, 2, null, null);
        return true;
    }

    private static bool TryConvertNode(
        JsonElement element,
        out NodeSchema node,
        out CanvasValidationIssue issue)
    {
        node = default!;
        issue = default!;

        if (element.ValueKind != JsonValueKind.Object)
        {
            issue = new CanvasValidationIssue("COZE_NODE_INVALID", "Coze 节点必须为对象。");
            return false;
        }

        var key = TryGetString(element, "id");
        if (string.IsNullOrWhiteSpace(key))
        {
            issue = new CanvasValidationIssue("COZE_NODE_ID_MISSING", "Coze 节点缺少 id。");
            return false;
        }

        if (!TryResolveNodeType(element, out var nodeType))
        {
            issue = new CanvasValidationIssue("COZE_NODE_TYPE_INVALID", $"Coze 节点 '{key}' 的 type 无法映射。", key);
            return false;
        }

        var label = TryGetNestedString(element, "data", "nodeMeta", "title")
            ?? TryGetNestedString(element, "data", "title")
            ?? key;

        var config = BuildNodeConfig(element, nodeType, key);
        var layout = BuildNodeLayout(element);
        CanvasSchema? childCanvas = null;
        if (TryGetProperty(element, "blocks", out var blocksElement) &&
            blocksElement.ValueKind == JsonValueKind.Array &&
            TryGetProperty(element, "edges", out var childEdgesElement) &&
            childEdgesElement.ValueKind == JsonValueKind.Array &&
            blocksElement.GetArrayLength() > 0)
        {
            if (!TryCompileCanvas(blocksElement, childEdgesElement, out childCanvas, out issue))
            {
                issue = issue with
                {
                    NodeKey = key,
                    Message = $"Coze 节点 '{key}' 子画布解析失败：{issue.Message}"
                };
                return false;
            }
        }

        node = new NodeSchema(
            key,
            nodeType,
            label,
            config,
            layout,
            childCanvas);
        return true;
    }

    private static bool TryConvertEdge(
        JsonElement element,
        out ConnectionSchema connection,
        out CanvasValidationIssue issue)
    {
        connection = default!;
        issue = default!;

        if (element.ValueKind != JsonValueKind.Object)
        {
            issue = new CanvasValidationIssue("COZE_EDGE_INVALID", "Coze 连线必须为对象。");
            return false;
        }

        var sourceNodeKey = TryGetString(element, "sourceNodeID")
            ?? TryGetString(element, "source")
            ?? TryGetString(element, "fromNode");
        if (string.IsNullOrWhiteSpace(sourceNodeKey))
        {
            issue = new CanvasValidationIssue("COZE_EDGE_SOURCE_MISSING", "Coze 连线缺少 sourceNodeID/source。");
            return false;
        }

        var targetNodeKey = TryGetString(element, "targetNodeID")
            ?? TryGetString(element, "target")
            ?? TryGetString(element, "toNode");
        if (string.IsNullOrWhiteSpace(targetNodeKey))
        {
            issue = new CanvasValidationIssue("COZE_EDGE_TARGET_MISSING", "Coze 连线缺少 targetNodeID/target。");
            return false;
        }

        var sourcePort = TryGetString(element, "sourcePortID")
            ?? TryGetString(element, "sourcePort")
            ?? TryGetString(element, "fromPort")
            ?? "output";
        var targetPort = TryGetString(element, "targetPortID")
            ?? TryGetString(element, "targetPort")
            ?? TryGetString(element, "toPort")
            ?? "input";

        connection = new ConnectionSchema(
            sourceNodeKey,
            sourcePort,
            targetNodeKey,
            targetPort,
            ResolveEdgeCondition(sourcePort));
        return true;
    }

    private static Dictionary<string, JsonElement> BuildNodeConfig(
        JsonElement element,
        WorkflowNodeType nodeType,
        string nodeKey)
    {
        var config = CloneDictionary(BuiltInWorkflowNodeDeclarations.GetDefaultConfig(nodeType));

        if (!TryGetProperty(element, "data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
        {
            return config;
        }

        var inputMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (TryGetProperty(dataElement, "inputs", out var inputsElement) && inputsElement.ValueKind == JsonValueKind.Object)
        {
            config["inputs"] = inputsElement.Clone();
            MergeGenericInputConfig(inputsElement, config);
            BuildInputMappings(inputsElement, inputMappings);
        }

        CozeNodeConfigAdapterRegistry.Adapt(
            nodeType,
            new CozeNodeAdaptContext(element, dataElement, nodeKey, inputMappings),
            config);

        if (inputMappings.Count > 0)
        {
            config["inputMappings"] = JsonSerializer.SerializeToElement(inputMappings);
        }

        return config;
    }

    private static void MergeGenericInputConfig(JsonElement inputsElement, Dictionary<string, JsonElement> config)
    {
        foreach (var property in inputsElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "inputParameters", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            config[property.Name] = property.Value.Clone();
        }
    }

    private static void BuildInputMappings(JsonElement inputsElement, Dictionary<string, string> mappings)
    {
        if (!TryGetProperty(inputsElement, "inputParameters", out var inputParameters) ||
            inputParameters.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var param in inputParameters.EnumerateArray())
        {
            if (!TryTrim(TryGetString(param, "name"), out var field) ||
                !TryGetProperty(param, "input", out var inputElement) ||
                inputElement.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(inputElement, "value", out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.Object ||
                !TryTrim(TryGetString(valueElement, "type"), out var valueType))
            {
                continue;
            }

            if (string.Equals(valueType, "ref", StringComparison.OrdinalIgnoreCase) &&
                TryGetProperty(valueElement, "content", out var contentElement) &&
                contentElement.ValueKind == JsonValueKind.Object &&
                TryTrim(TryGetString(contentElement, "blockID"), out var blockId) &&
                TryTrim(TryGetString(contentElement, "name"), out var name))
            {
                mappings[field] = $"{blockId}.{name}";
            }
        }
    }

    private static string? ResolveEdgeCondition(string sourcePort)
    {
        if (string.IsNullOrWhiteSpace(sourcePort))
        {
            return null;
        }

        var normalized = sourcePort.Trim().ToLowerInvariant();
        if (normalized == "true" || normalized.StartsWith("true_", StringComparison.Ordinal))
        {
            return "selector_result == true";
        }

        if (normalized == "false" || normalized.StartsWith("false_", StringComparison.Ordinal))
        {
            return "selector_result == false";
        }

        if (normalized == "default")
        {
            return "default";
        }

        if (normalized == "branch_error")
        {
            return "error";
        }

        if (normalized.StartsWith("branch_", StringComparison.Ordinal))
        {
            return normalized;
        }

        return null;
    }

    private static NodeLayout BuildNodeLayout(JsonElement element)
    {
        if (TryGetNestedProperty(element, out var positionElement, "meta", "position") &&
            positionElement.ValueKind == JsonValueKind.Object)
        {
            return new NodeLayout(
                TryGetDouble(positionElement, "x") ?? 0,
                TryGetDouble(positionElement, "y") ?? 0,
                160,
                60);
        }

        return new NodeLayout(0, 0, 160, 60);
    }

    private static bool TryResolveNodeType(JsonElement element, out WorkflowNodeType nodeType)
    {
        nodeType = default;
        if (!TryGetProperty(element, "type", out var typeElement))
        {
            return false;
        }

        if (typeElement.ValueKind == JsonValueKind.Number && typeElement.TryGetInt32(out var numericType))
        {
            return CozeNodeTypeMap.TryGetValue(
                numericType.ToString(CultureInfo.InvariantCulture),
                out nodeType);
        }

        if (typeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var raw = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (CozeNodeTypeMap.TryGetValue(trimmed, out nodeType))
        {
            return true;
        }

        if (Enum.TryParse<WorkflowNodeType>(trimmed, true, out var parsedType) &&
            Enum.IsDefined(typeof(WorkflowNodeType), parsedType))
        {
            nodeType = parsedType;
            return true;
        }

        return false;
    }

    internal static string? ExtractFirstExpressionContent(JsonElement inputParametersElement)
    {
        foreach (var item in inputParametersElement.EnumerateArray())
        {
            if (!TryGetProperty(item, "input", out var inputElement) || inputElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryGetProperty(inputElement, "value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryGetProperty(valueElement, "content", out var contentElement) || contentElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var content = contentElement.GetString();
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }

        return null;
    }

    private static Dictionary<string, JsonElement> CloneDictionary(IReadOnlyDictionary<string, JsonElement>? source)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return result;
        }

        foreach (var (key, value) in source)
        {
            result[key] = value.Clone();
        }

        return result;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetNestedProperty(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !TryGetProperty(value, segment, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private static string? TryGetNestedString(JsonElement element, params string[] path)
    {
        if (!TryGetNestedProperty(element, out var value, path) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static bool TryTrim(string? value, out string trimmed)
    {
        trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length > 0;
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetDouble(out var result) ? result : null;
    }

    private static CozeWorkflowCompileResult Fail(string code, string message)
    {
        return new CozeWorkflowCompileResult(
            false,
            null,
            [new CanvasValidationIssue(code, message)]);
    }
}
