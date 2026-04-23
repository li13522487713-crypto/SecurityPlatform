using System.Text.Json;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

public static class WorkflowCanvasJsonBridge
{
    private static readonly CozeWorkflowPlanCompiler CozePlanCompiler = new();

    public static bool TryParseCanvas(string canvasJson, out CanvasSchema? canvas)
    {
        canvas = null;
        if (string.IsNullOrWhiteSpace(canvasJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(canvasJson);
            if (TryConvertCanvas(document.RootElement, out canvas))
            {
                return true;
            }
        }
        catch
        {
            canvas = null;
        }

        var compileResult = CozePlanCompiler.Compile(canvasJson);
        if (compileResult.IsSuccess && compileResult.Canvas is not null)
        {
            canvas = compileResult.Canvas;
            return true;
        }

        canvas = null;
        return false;
    }
    private static bool TryConvertCanvas(JsonElement root, out CanvasSchema? canvas)
    {
        canvas = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetProperty(root, "nodes", out var nodesElement) || nodesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        if (!TryGetProperty(root, "connections", out var connectionsElement) || connectionsElement.ValueKind != JsonValueKind.Array)
        {
            if (!TryGetProperty(root, "edges", out connectionsElement) || connectionsElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
        }

        var nodes = new List<NodeSchema>();
        foreach (var nodeElement in nodesElement.EnumerateArray())
        {
            if (!TryConvertNode(nodeElement, out var node))
            {
                return false;
            }

            nodes.Add(node);
        }

        var connections = new List<ConnectionSchema>();
        foreach (var connectionElement in connectionsElement.EnumerateArray())
        {
            if (!TryConvertConnection(connectionElement, out var connection))
            {
                return false;
            }

            connections.Add(connection);
        }

        var schemaVersion = TryGetInt32(root, "schemaVersion") ?? 2;
        var viewport = TryConvertViewport(root);
        var globals = TryGetJsonObject(root, "globals");

        canvas = new CanvasSchema(nodes, connections, schemaVersion, viewport, globals);
        return true;
    }

    private static bool TryConvertNode(JsonElement element, out NodeSchema node)
    {
        node = default!;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var key = TryGetString(element, "key");
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!TryResolveNodeType(element, out var nodeType))
        {
            return false;
        }

        var label = TryGetString(element, "label")
            ?? TryGetString(element, "title")
            ?? key;
        var config = TryGetJsonObject(element, "config")
            ?? TryGetJsonObject(element, "configs")
            ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (TryGetJsonObject(element, "inputMappings") is { Count: > 0 } inputMappings &&
            !config.ContainsKey("inputMappings"))
        {
            config["inputMappings"] = JsonSerializer.SerializeToElement(inputMappings);
        }

        var layout = TryConvertLayout(element)
            ?? new NodeLayout(0, 0, 160, 60);

        CanvasSchema? childCanvas = null;
        if (TryGetProperty(element, "childCanvas", out var childCanvasElement) &&
            childCanvasElement.ValueKind != JsonValueKind.Null &&
            childCanvasElement.ValueKind != JsonValueKind.Undefined)
        {
            if (!TryConvertCanvas(childCanvasElement, out childCanvas))
            {
                return false;
            }
        }

        var inputTypes = TryGetStringDictionary(element, "inputTypes");
        var outputTypes = TryGetStringDictionary(element, "outputTypes");
        var inputSources = TryGetNodeFieldMappings(element, "inputSources");
        var outputSources = TryGetNodeFieldMappings(element, "outputSources");
        var ports = TryGetPorts(element, "ports");
        var version = TryGetString(element, "version");
        var debugMeta = TryGetJsonObject(element, "debugMeta");

        node = new NodeSchema(
            key,
            nodeType,
            label,
            config,
            layout,
            childCanvas,
            inputTypes,
            outputTypes,
            inputSources,
            outputSources,
            ports,
            version,
            debugMeta);
        return true;
    }

    private static bool TryConvertConnection(JsonElement element, out ConnectionSchema connection)
    {
        connection = default!;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var sourceNodeKey = TryGetString(element, "sourceNodeKey")
            ?? TryGetString(element, "fromNode");
        var targetNodeKey = TryGetString(element, "targetNodeKey")
            ?? TryGetString(element, "toNode");

        if (string.IsNullOrWhiteSpace(sourceNodeKey) || string.IsNullOrWhiteSpace(targetNodeKey))
        {
            return false;
        }

        var sourcePort = TryGetString(element, "sourcePort")
            ?? TryGetString(element, "fromPort")
            ?? "output";
        var targetPort = TryGetString(element, "targetPort")
            ?? TryGetString(element, "toPort")
            ?? "input";
        var condition = TryGetString(element, "condition");

        connection = new ConnectionSchema(
            sourceNodeKey,
            sourcePort,
            targetNodeKey,
            targetPort,
            string.IsNullOrWhiteSpace(condition) ? null : condition);
        return true;
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

        if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var numericFromText))
        {
            var fromNumeric = (WorkflowNodeType)numericFromText;
            if (Enum.IsDefined(typeof(WorkflowNodeType), fromNumeric))
            {
                nodeType = fromNumeric;
                return true;
            }
        }

        if (Enum.TryParse<WorkflowNodeType>(trimmed, true, out var parsedType)
            && Enum.IsDefined(typeof(WorkflowNodeType), parsedType))
        {
            nodeType = parsedType;
            return true;
        }

        return false;
    }

    private static NodeLayout? TryConvertLayout(JsonElement element)
    {
        if (!TryGetProperty(element, "layout", out var layoutElement) || layoutElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new NodeLayout(
            TryGetDouble(layoutElement, "x") ?? 0,
            TryGetDouble(layoutElement, "y") ?? 0,
            TryGetDouble(layoutElement, "width") ?? 160,
            TryGetDouble(layoutElement, "height") ?? 60);
    }

    private static ViewportState? TryConvertViewport(JsonElement element)
    {
        if (!TryGetProperty(element, "viewport", out var viewportElement) || viewportElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ViewportState(
            TryGetDouble(viewportElement, "x") ?? 0,
            TryGetDouble(viewportElement, "y") ?? 0,
            TryGetDouble(viewportElement, "zoom") ?? 100);
    }

    private static Dictionary<string, JsonElement>? TryGetJsonObject(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }

        return result;
    }

    private static Dictionary<string, string>? TryGetStringDictionary(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var text = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result[property.Name] = text.Trim();
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<NodeFieldMapping>? TryGetNodeFieldMappings(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var mappings = new List<NodeFieldMapping>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var field = TryGetString(item, "field");
            var path = TryGetString(item, "path");
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            mappings.Add(new NodeFieldMapping(field, path, TryGetString(item, "defaultValue")));
        }

        return mappings.Count == 0 ? null : mappings;
    }

    private static IReadOnlyList<NodePortSchema>? TryGetPorts(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var ports = new List<NodePortSchema>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = TryGetString(item, "key");
            var name = TryGetString(item, "name");
            var direction = TryGetString(item, "direction");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(direction))
            {
                continue;
            }

            ports.Add(new NodePortSchema(
                key,
                name,
                direction,
                TryGetString(item, "dataType"),
                TryGetBoolean(item, "isRequired"),
                TryGetInt32(item, "maxConnections")));
        }

        return ports.Count == 0 ? null : ports;
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

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static bool? TryGetBoolean(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var result) ? result : null;
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetDouble(out var result) ? result : null;
    }
}
