using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.AiPlatform;

namespace Atlas.SecurityPlatform.Tests.Workflows;

public sealed class CozeWorkflowPlanCompilerTests
{
    private readonly CozeWorkflowPlanCompiler _compiler = new();

    [Fact]
    public void Compile_ShouldAcceptCozeNativeCanvas()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": 1,
                  "meta": { "position": { "x": 120, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "开始" },
                    "outputs": [{ "name": "incident", "type": "string", "required": true }]
                  }
                },
                {
                  "id": "code_1",
                  "type": 5,
                  "meta": { "position": { "x": 360, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "代码执行" },
                    "inputs": {
                      "language": "javascript",
                      "code": "function main(args){ return { result: args.params.incident }; }",
                      "inputParameters": [{ "name": "incident" }]
                    },
                    "outputs": [{ "name": "result", "type": "string" }]
                  }
                },
                {
                  "id": "exit_1",
                  "type": 2,
                  "meta": { "position": { "x": 660, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "结束" },
                    "inputs": {
                      "terminatePlan": "returnVariables",
                      "inputParameters": [
                        {
                          "name": "result",
                          "input": { "value": { "content": "{{code_1.result}}" } }
                        }
                      ]
                    }
                  }
                }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "code_1" },
                { "source": "code_1", "target": "exit_1" }
              ]
            }
            """;

        var result = _compiler.Compile(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Canvas);
        Assert.Equal(3, result.Canvas!.Nodes.Count);
        Assert.Equal(WorkflowNodeType.Entry, result.Canvas.Nodes[0].Type);
        Assert.Equal("incident", result.Canvas.Nodes[0].Config["entryVariable"].GetString());
        Assert.Equal(WorkflowNodeType.CodeRunner, result.Canvas.Nodes[1].Type);
        Assert.Equal("result", result.Canvas.Nodes[1].Config["outputKey"].GetString());
        Assert.Equal("{{code_1.result}}", result.Canvas.Nodes[2].Config["exitTemplate"].GetString());
    }

    [Fact]
    public void Compile_ShouldRejectNodeWithoutId()
    {
        const string json = """
            {
              "nodes": [
                {
                  "type": 1,
                  "meta": { "position": { "x": 0, "y": 0 } },
                  "data": { "nodeMeta": { "title": "开始" } }
                }
              ],
              "edges": []
            }
            """;

        var result = _compiler.Compile(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "COZE_NODE_ID_MISSING");
    }

    [Fact]
    public void Compile_ShouldRejectUnknownNodeType()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": "NoSuchType",
                  "meta": { "position": { "x": 0, "y": 0 } },
                  "data": { "nodeMeta": { "title": "开始" } }
                }
              ],
              "edges": []
            }
            """;

        var result = _compiler.Compile(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "COZE_NODE_TYPE_INVALID");
    }

    [Fact]
    public void Compile_ResultCanvas_ShouldBeAcceptedByAtlasValidator()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": 1,
                  "meta": { "position": { "x": 120, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "开始" },
                    "outputs": [{ "name": "incident", "type": "string", "required": true }]
                  }
                },
                {
                  "id": "exit_1",
                  "type": 2,
                  "meta": { "position": { "x": 360, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "结束" },
                    "inputs": {
                      "terminatePlan": "returnVariables",
                      "inputParameters": [
                        {
                          "name": "incident",
                          "input": { "value": { "content": "{{incident}}" } }
                        }
                      ]
                    }
                  }
                }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "exit_1" }
              ]
            }
            """;

        var compileResult = _compiler.Compile(json);
        Assert.True(compileResult.IsSuccess);
        var serialized = JsonSerializer.Serialize(compileResult.Canvas);
        var validator = new Atlas.Infrastructure.Services.WorkflowEngine.CanvasValidator();
        var validationResult = validator.ValidateCanvas(serialized);
        Assert.True(validationResult.IsValid);
    }
}
