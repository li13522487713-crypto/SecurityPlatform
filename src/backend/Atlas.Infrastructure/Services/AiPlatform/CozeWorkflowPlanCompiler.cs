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
    private static readonly IReadOnlyDictionary<string, WorkflowNodeType> CozeNodeTypeAliases =
        new Dictionary<string, WorkflowNodeType>(StringComparer.OrdinalIgnoreCase)
        {
            ["Start"] = WorkflowNodeType.Entry,
            ["Entry"] = WorkflowNodeType.Entry,
            ["End"] = WorkflowNodeType.Exit,
            ["Exit"] = WorkflowNodeType.Exit,
            ["Code"] = WorkflowNodeType.CodeRunner,
            ["CodeRunner"] = WorkflowNodeType.CodeRunner,
            ["If"] = WorkflowNodeType.Selector,
            ["Condition"] = WorkflowNodeType.Selector,
            ["Http"] = WorkflowNodeType.HttpRequester,
            ["HTTPRequest"] = WorkflowNodeType.HttpRequester
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

        var nodes = new List<NodeSchema>();
        foreach (var nodeElement in nodesElement.EnumerateArray())
        {
            if (!TryConvertNode(nodeElement, out var node, out var issue))
            {
                issues.Add(issue);
                errors = issues;
                return false;
            }

            nodes.Add(node);
        }

        var connections = new List<ConnectionSchema>();
        foreach (var edgeElement in edgesElement.EnumerateArray())
        {
            if (!TryConvertEdge(edgeElement, out var connection, out var issue))
            {
                issues.Add(issue);
                errors = issues;
                return false;
            }

            connections.Add(connection);
        }

        canvas = new CanvasSchema(nodes, connections, 2, null, null);
        errors = Array.Empty<CanvasValidationIssue>();
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

        node = new NodeSchema(
            key,
            nodeType,
            label,
            config,
            layout);
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

        connection = new ConnectionSchema(sourceNodeKey, "output", targetNodeKey, "input", null);
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

        if (TryGetProperty(dataElement, "inputs", out var inputsElement) && inputsElement.ValueKind == JsonValueKind.Object)
        {
            config["inputs"] = inputsElement.Clone();
        }

        switch (nodeType)
        {
            case WorkflowNodeType.Entry:
                if (TryGetProperty(dataElement, "outputs", out var outputsElement) &&
                    outputsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in outputsElement.EnumerateArray())
                    {
                        var outputName = TryGetString(item, "name");
                        if (!string.IsNullOrWhiteSpace(outputName))
                        {
                            config["entryVariable"] = JsonSerializer.SerializeToElement(outputName.Trim());
                            break;
                        }
                    }
                }
                break;
            case WorkflowNodeType.Exit:
                if (TryGetProperty(dataElement, "inputs", out var exitInputs) && exitInputs.ValueKind == JsonValueKind.Object)
                {
                    var terminatePlan = TryGetString(exitInputs, "terminatePlan");
                    if (!string.IsNullOrWhiteSpace(terminatePlan))
                    {
                        config["exitTerminateMode"] = JsonSerializer.SerializeToElement(terminatePlan.Trim());
                    }

                    if (TryGetProperty(exitInputs, "inputParameters", out var inputParameters) &&
                        inputParameters.ValueKind == JsonValueKind.Array)
                    {
                        var exitTemplate = ExtractFirstExpressionContent(inputParameters);
                        if (!string.IsNullOrWhiteSpace(exitTemplate))
                        {
                            config["exitTemplate"] = JsonSerializer.SerializeToElement(exitTemplate);
                        }
                    }
                }
                break;
            case WorkflowNodeType.CodeRunner:
                if (TryGetProperty(dataElement, "inputs", out var codeInputs) && codeInputs.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetProperty(codeInputs, "code", out var codeElement))
                    {
                        config["code"] = codeElement.Clone();
                    }

                    if (TryGetProperty(codeInputs, "language", out var languageElement))
                    {
                        config["language"] = languageElement.Clone();
                    }

                    if (TryGetProperty(codeInputs, "inputParameters", out var inputParameters) &&
                        inputParameters.ValueKind == JsonValueKind.Array)
                    {
                        config["inputParameters"] = inputParameters.Clone();
                    }
                }

                if (TryGetProperty(dataElement, "outputs", out var codeOutputs) &&
                    codeOutputs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in codeOutputs.EnumerateArray())
                    {
                        var outputName = TryGetString(item, "name");
                        if (!string.IsNullOrWhiteSpace(outputName))
                        {
                            config["outputKey"] = JsonSerializer.SerializeToElement(outputName.Trim());
                            break;
                        }
                    }
                }

                if (!config.ContainsKey("outputKey"))
                {
                    config["outputKey"] = JsonSerializer.SerializeToElement($"{nodeKey}_output");
                }
                break;
        }

        return config;
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
            nodeType = (WorkflowNodeType)numericType;
            return Enum.IsDefined(typeof(WorkflowNodeType), nodeType);
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
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericFromText))
        {
            var fromNumeric = (WorkflowNodeType)numericFromText;
            if (Enum.IsDefined(typeof(WorkflowNodeType), fromNumeric))
            {
                nodeType = fromNumeric;
                return true;
            }
        }

        if (Enum.TryParse<WorkflowNodeType>(trimmed, true, out var parsedType) &&
            Enum.IsDefined(typeof(WorkflowNodeType), parsedType))
        {
            nodeType = parsedType;
            return true;
        }

        return CozeNodeTypeAliases.TryGetValue(trimmed, out nodeType);
    }

    private static string? ExtractFirstExpressionContent(JsonElement inputParametersElement)
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
