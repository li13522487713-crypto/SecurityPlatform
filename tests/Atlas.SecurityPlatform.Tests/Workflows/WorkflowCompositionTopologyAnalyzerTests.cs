using System.Reflection;

namespace Atlas.SecurityPlatform.Tests.Workflows;

/// <summary>
/// M19 S19-4 IO 推断算法单测。
///
/// 由于 WorkflowCompositionTopologyAnalyzer 是 internal static，本测试通过反射调用，
/// 不修改产线类型可见性，亦不影响生产构建零警告。
/// </summary>
public sealed class WorkflowCompositionTopologyAnalyzerTests
{
    private static (IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs) Analyze(string canvasJson, IReadOnlyList<string> selected)
    {
        var asm = typeof(Atlas.Infrastructure.Services.LowCode.WorkflowCompositionService).Assembly;
        var t = asm.GetType("Atlas.Infrastructure.Services.LowCode.WorkflowCompositionTopologyAnalyzer", throwOnError: true)!;
        var m = t.GetMethod("Analyze", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = m.Invoke(null, new object[] { canvasJson, selected })!;
        var rt = result.GetType();
        var inputsField = rt.GetField("Item1")!;
        var outputsField = rt.GetField("Item2")!;
        return ((IReadOnlyList<string>)inputsField.GetValue(result)!, (IReadOnlyList<string>)outputsField.GetValue(result)!);
    }

    [Fact]
    public void Analyze_NoEdges_FallsBackToSinglePort()
    {
        var canvas = "{}";
        var (inputs, outputs) = Analyze(canvas, new[] { "n1" });
        Assert.Single(inputs);
        Assert.Single(outputs);
    }

    [Fact]
    public void Analyze_BoundaryEdges_ProducesOnlyCrossingPorts()
    {
        // 选中 {b, c}：a→b 入边（外 → 内）入参 = 'x'；c→d 出边（内 → 外）出参 = 'y'；
        // b→c 是子集内部边，不计入。
        var canvas = """
        {
          "nodes": [{"key":"a"},{"key":"b"},{"key":"c"},{"key":"d"}],
          "edges": [
            {"source":"a","target":"b","targetPort":"x"},
            {"source":"b","target":"c"},
            {"source":"c","target":"d","sourcePort":"y"}
          ]
        }
        """;
        var (inputs, outputs) = Analyze(canvas, new[] { "b", "c" });
        Assert.Equal(new[] { "x" }, inputs);
        Assert.Equal(new[] { "y" }, outputs);
    }

    [Fact]
    public void Analyze_DefaultPortNames_WhenPortFieldsMissing()
    {
        var canvas = """
        {
          "edges": [
            {"source":"a","target":"b"},
            {"source":"b","target":"d"}
          ]
        }
        """;
        var (inputs, outputs) = Analyze(canvas, new[] { "b" });
        Assert.Equal(new[] { "input" }, inputs);
        Assert.Equal(new[] { "output" }, outputs);
    }

    [Fact]
    public void Analyze_AlternateEdgeFieldNames_AreSupported()
    {
        // 兼容 from/to + sourceNodeKey/targetNodeKey 命名
        var canvas = """
        {
          "edges": [
            {"sourceNodeKey":"a","targetNodeKey":"b","targetField":"alpha"},
            {"from":"b","to":"c","sourceField":"beta"}
          ]
        }
        """;
        var (inputs, outputs) = Analyze(canvas, new[] { "b" });
        Assert.Equal(new[] { "alpha" }, inputs);
        Assert.Equal(new[] { "beta" }, outputs);
    }

    [Fact]
    public void Analyze_DuplicatePorts_AreDeduplicatedAndSorted()
    {
        var canvas = """
        {
          "edges": [
            {"source":"a","target":"b","targetPort":"x"},
            {"source":"a","target":"c","targetPort":"x"},
            {"source":"e","target":"b","targetPort":"a"}
          ]
        }
        """;
        var (inputs, _) = Analyze(canvas, new[] { "b", "c" });
        Assert.Equal(new[] { "a", "x" }, inputs);
    }

    [Fact]
    public void Analyze_MalformedCanvas_FallsBackToSinglePort()
    {
        var (inputs, outputs) = Analyze("not-json", new[] { "n1" });
        Assert.Single(inputs);
        Assert.Single(outputs);
    }
}
