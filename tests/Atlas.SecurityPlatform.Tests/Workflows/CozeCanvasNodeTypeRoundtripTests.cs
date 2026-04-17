using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.SecurityPlatform.Tests.Workflows;

/// <summary>
/// 风险 3 回归测试：节点 ID 双轨制反向归一。
///
/// Coze playground 把节点 type 序列化为字符串数字（如 <c>"3"</c>=Llm / <c>"45"</c>=HttpRequester），
/// 后端必须能在 SaveDraft 之后的 Run / DebugNode 路径上把它正确还原为 <see cref="WorkflowNodeType"/>，
/// 否则 <see cref="DagExecutor"/> 找不到执行器。
///
/// 本组测试直接覆盖 <see cref="WorkflowCanvasJsonBridge"/> 的反序列化语义，
/// 同时覆盖三种合法 type 形态（int / "int" / "Enum.Name"）。
/// </summary>
public sealed class CozeCanvasNodeTypeRoundtripTests
{
    private const string CanvasJsonTemplate = """
        {
          "nodes": [
            { "key": "entry_1", "type": __ENTRY_TYPE__, "label": "开始", "config": {}, "layout": { "x": 0, "y": 0, "width": 160, "height": 60 } },
            { "key": "llm_1", "type": __LLM_TYPE__, "label": "LLM", "config": {}, "layout": { "x": 200, "y": 0, "width": 160, "height": 60 } }
          ],
          "connections": [
            { "sourceNodeKey": "entry_1", "sourcePort": "output", "targetNodeKey": "llm_1", "targetPort": "input" }
          ]
        }
        """;

    [Theory]
    [InlineData("1", "3")]
    [InlineData("\"1\"", "\"3\"")]
    [InlineData("\"Entry\"", "\"Llm\"")]
    [InlineData("1", "\"3\"")]
    [InlineData("\"1\"", "\"Llm\"")]
    public void TryParseCanvas_ShouldAcceptInt_StringInt_AndEnumName(string entryType, string llmType)
    {
        var json = CanvasJsonTemplate
            .Replace("__ENTRY_TYPE__", entryType)
            .Replace("__LLM_TYPE__", llmType);

        var ok = WorkflowCanvasJsonBridge.TryParseCanvas(json, out var canvas);

        Assert.True(ok, $"画布解析应当成功，原始 type 形态: entry={entryType}, llm={llmType}");
        Assert.NotNull(canvas);
        Assert.Equal(2, canvas!.Nodes.Count);
        Assert.Equal(WorkflowNodeType.Entry, canvas.Nodes[0].Type);
        Assert.Equal(WorkflowNodeType.Llm, canvas.Nodes[1].Type);
    }

    [Fact]
    public void TryParseCanvas_ShouldRejectUnknownNodeType()
    {
        var json = CanvasJsonTemplate
            .Replace("__ENTRY_TYPE__", "1")
            .Replace("__LLM_TYPE__", "\"NoSuchType\"");

        var ok = WorkflowCanvasJsonBridge.TryParseCanvas(json, out var canvas);

        Assert.False(ok);
        Assert.Null(canvas);
    }

    [Fact]
    public void NormalizeToBackendCanvasJson_ShouldOutputIntType()
    {
        var json = CanvasJsonTemplate
            .Replace("__ENTRY_TYPE__", "\"1\"")
            .Replace("__LLM_TYPE__", "\"Llm\"");

        var normalized = WorkflowCanvasJsonBridge.NormalizeToBackendCanvasJson(json);

        using var doc = JsonDocument.Parse(normalized);
        var nodes = doc.RootElement.GetProperty("nodes");
        Assert.Equal(JsonValueKind.Number, nodes[0].GetProperty("type").ValueKind);
        Assert.Equal((int)WorkflowNodeType.Entry, nodes[0].GetProperty("type").GetInt32());
        Assert.Equal((int)WorkflowNodeType.Llm, nodes[1].GetProperty("type").GetInt32());
    }
}
