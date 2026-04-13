using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.SecurityPlatform.Tests.Services;

/// <summary>
/// TS-08: CanvasSchema 序列化/反序列化往返测试。
/// 确保 JSON → CanvasSchema → JSON 字段名、枚举值、嵌套结构不丢失。
/// </summary>
public sealed class SchemaRoundtripTests
{
    // ─── 辅助：最小合法节点 JSON ────────────────────────────────────────────
    private static string MakeNodeJson(
        string key, int type, string label = "Node",
        string? childCanvas = null)
    {
        var child = childCanvas is null ? "" : $", \"childCanvas\": {childCanvas}";
        return $$"""
            {
              "key": "{{key}}",
              "type": {{type}},
              "label": "{{label}}",
              "config": {},
              "layout": { "x": 0, "y": 0, "width": 120, "height": 40 }
              {{child}}
            }
            """;
    }

    private static string MakeConnJson(
        string src, string srcPort, string tgt, string tgtPort, string? condition = null)
    {
        var cond = condition is null ? "null" : $"\"{condition}\"";
        return $$"""
            {
              "sourceNodeKey": "{{src}}",
              "sourcePort": "{{srcPort}}",
              "targetNodeKey": "{{tgt}}",
              "targetPort": "{{tgtPort}}",
              "condition": {{cond}}
            }
            """;
    }

    // ─── ParseCanvas ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseCanvas_LinearGraph_ShouldReturnCorrectNodesAndConnections()
    {
        var json = $$"""
            {
              "nodes": [
                {{MakeNodeJson("entry_1", 1, "Entry")}},
                {{MakeNodeJson("text_1", 15, "Text")}},
                {{MakeNodeJson("exit_1", 2, "Exit")}}
              ],
              "connections": [
                {{MakeConnJson("entry_1", "output", "text_1", "input")}},
                {{MakeConnJson("text_1", "output", "exit_1", "input")}}
              ]
            }
            """;

        var canvas = DagExecutor.ParseCanvas(json);

        Assert.NotNull(canvas);
        Assert.Equal(3, canvas.Nodes.Count);
        Assert.Equal(2, canvas.Connections.Count);
        Assert.Equal("entry_1", canvas.Nodes[0].Key);
        Assert.Equal(WorkflowNodeType.Entry, canvas.Nodes[0].Type);
        Assert.Equal("exit_1", canvas.Nodes[2].Key);
        Assert.Equal(WorkflowNodeType.Exit, canvas.Nodes[2].Type);
    }

    [Fact]
    public void ParseCanvas_WithNodeConfig_ShouldPreserveConfigData()
    {
        var json = $$"""
            {
              "nodes": [
                {
                  "key": "llm_1",
                  "type": 3,
                  "label": "LLM",
                  "config": {
                    "model": "gpt-4",
                    "temperature": 0.7
                  },
                  "layout": { "x": 100, "y": 100, "width": 120, "height": 40 }
                }
              ],
              "connections": []
            }
            """;

        var canvas = DagExecutor.ParseCanvas(json);

        Assert.NotNull(canvas);
        var node = Assert.Single(canvas.Nodes);
        Assert.Equal("llm_1", node.Key);
        Assert.True(node.Config.ContainsKey("model"));
        Assert.True(node.Config.ContainsKey("temperature"));
    }

    [Fact]
    public void ParseCanvas_WithConnectionCondition_ShouldPreserveCondition()
    {
        var json = $$"""
            {
              "nodes": [
                {{MakeNodeJson("entry_1", 1)}},
                {{MakeNodeJson("selector_1", 8)}},
                {{MakeNodeJson("branch_a", 15)}}
              ],
              "connections": [
                {{MakeConnJson("entry_1", "output", "selector_1", "input")}},
                {{MakeConnJson("selector_1", "true", "branch_a", "input", "true")}}
              ]
            }
            """;

        var canvas = DagExecutor.ParseCanvas(json);

        Assert.NotNull(canvas);
        var condConn = canvas.Connections.FirstOrDefault(c => c.Condition == "true");
        Assert.NotNull(condConn);
        Assert.Equal("selector_1", condConn.SourceNodeKey);
        Assert.Equal("true", condConn.SourcePort);
    }

    [Fact]
    public void ParseCanvas_EmptyNodes_ShouldReturnEmptyCollections()
    {
        const string json = """{ "nodes": [], "connections": [] }""";
        var canvas = DagExecutor.ParseCanvas(json);
        Assert.NotNull(canvas);
        Assert.Empty(canvas.Nodes);
        Assert.Empty(canvas.Connections);
    }

    [Fact]
    public void ParseCanvas_NullOrEmpty_ShouldReturnNull()
    {
        Assert.Null(DagExecutor.ParseCanvas(null!));
        Assert.Null(DagExecutor.ParseCanvas(""));
        Assert.Null(DagExecutor.ParseCanvas("   "));
    }

    [Fact]
    public void ParseCanvas_InvalidJson_ShouldReturnNull()
    {
        Assert.Null(DagExecutor.ParseCanvas("not-valid-json"));
    }

    [Fact]
    public void ParseCanvas_WithSchemaVersion_ShouldParseCorrectly()
    {
        var json = $$"""
            {
              "schemaVersion": 3,
              "nodes": [
                {{MakeNodeJson("entry_1", 1)}},
                {{MakeNodeJson("exit_1", 2)}}
              ],
              "connections": [
                {{MakeConnJson("entry_1", "output", "exit_1", "input")}}
              ]
            }
            """;

        var canvas = DagExecutor.ParseCanvas(json);

        Assert.NotNull(canvas);
        Assert.Equal(3, canvas.SchemaVersion);
        Assert.Equal(2, canvas.Nodes.Count);
    }

    [Fact]
    public void ParseCanvas_WithChildCanvas_ShouldParseNestedCanvas()
    {
        var innerCanvas = $$"""
            {
              "nodes": [ {{MakeNodeJson("text_inner", 15, "Inner")}} ],
              "connections": []
            }
            """;

        var json = $$"""
            {
              "nodes": [
                {{MakeNodeJson("loop_1", 21, "Loop", innerCanvas)}}
              ],
              "connections": []
            }
            """;

        var canvas = DagExecutor.ParseCanvas(json);

        Assert.NotNull(canvas);
        var loopNode = Assert.Single(canvas.Nodes);
        Assert.NotNull(loopNode.ChildCanvas);
        Assert.Single(loopNode.ChildCanvas.Nodes);
        Assert.Equal("text_inner", loopNode.ChildCanvas.Nodes[0].Key);
    }

    [Fact]
    public void ParseCanvas_WithEditorCanvasPayload_ShouldNormalizeToRuntimeSchema()
    {
        var json = """
            {
              "nodes": [
                {
                  "key": "entry_1",
                  "type": "Entry",
                  "title": "开始",
                  "configs": {
                    "entryVariable": "incident"
                  },
                  "inputMappings": {
                    "incident": "input.incident"
                  },
                  "layout": { "x": 120, "y": 80, "width": 160, "height": 60 }
                },
                {
                  "key": "text_1",
                  "type": "TextProcessor",
                  "title": "文本处理",
                  "configs": {
                    "template": "{{incident}}",
                    "outputKey": "result"
                  },
                  "layout": { "x": 360, "y": 80, "width": 220, "height": 80 }
                }
              ],
              "connections": [
                {
                  "fromNode": "entry_1",
                  "fromPort": "output",
                  "toNode": "text_1",
                  "toPort": "input",
                  "condition": null
                }
              ]
            }
            """;

        var canvas = DagExecutor.ParseCanvas(json);

        Assert.NotNull(canvas);
        Assert.Equal(2, canvas.Nodes.Count);
        Assert.Equal(WorkflowNodeType.Entry, canvas.Nodes[0].Type);
        Assert.Equal("开始", canvas.Nodes[0].Label);
        Assert.True(canvas.Nodes[0].Config.ContainsKey("entryVariable"));
        Assert.True(canvas.Nodes[0].Config.ContainsKey("inputMappings"));
        Assert.Single(canvas.Connections);
        Assert.Equal("entry_1", canvas.Connections[0].SourceNodeKey);
        Assert.Equal("text_1", canvas.Connections[0].TargetNodeKey);
    }

    // ─── Roundtrip: serialize → deserialize ──────────────────────────────────

    [Fact]
    public void Roundtrip_SerializeAndDeserialize_ShouldPreserveFields()
    {
        var original = new CanvasSchema(
            Nodes: new[]
            {
                new NodeSchema(
                    Key: "entry_1",
                    Type: WorkflowNodeType.Entry,
                    Label: "Start",
                    Config: new Dictionary<string, JsonElement>(),
                    Layout: new NodeLayout(0, 0, 120, 40))
            },
            Connections: Array.Empty<ConnectionSchema>(),
            SchemaVersion: 2);

        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(original, opts);
        var restored = JsonSerializer.Deserialize<CanvasSchema>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(restored);
        Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.Nodes.Count, restored.Nodes.Count);
        Assert.Equal(original.Nodes[0].Key, restored.Nodes[0].Key);
        Assert.Equal(original.Nodes[0].Type, restored.Nodes[0].Type);
    }
}
