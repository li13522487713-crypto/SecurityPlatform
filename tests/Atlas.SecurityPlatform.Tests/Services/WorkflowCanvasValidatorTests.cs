using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class WorkflowCanvasValidatorTests
{
    private readonly CanvasValidator _validator = new();

    [Fact]
    public void ValidateCanvas_ShouldAcceptDynamicPortsAndRequiredInputBindings()
    {
        var canvas = new CanvasSchema(
            Nodes:
            [
                new NodeSchema(
                    "entry_1",
                    WorkflowNodeType.Entry,
                    "entry_1",
                    new Dictionary<string, JsonElement>(),
                    new NodeLayout(0, 0, 120, 60),
                    Ports:
                    [
                        new NodePortSchema("out_custom", "Out", "output", "string", false, 2)
                    ]),
                new NodeSchema(
                    "receiver_1",
                    WorkflowNodeType.OutputEmitter,
                    "receiver_1",
                    new Dictionary<string, JsonElement>(),
                    new NodeLayout(220, 0, 120, 60),
                    Ports:
                    [
                        new NodePortSchema("in_custom", "In", "input", "string", true, 1)
                    ])
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "out_custom", "receiver_1", "in_custom", null)
            ]);

        var result = _validator.ValidateCanvas(JsonSerializer.Serialize(canvas));

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.Code is "UNKNOWN_SOURCE_PORT" or "UNKNOWN_TARGET_PORT" or "REQUIRED_INPUT_PORT_UNBOUND");
    }

    [Fact]
    public void ValidateCanvas_ShouldReportSelectorBranchConfigurationErrors()
    {
        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode("selector_1", WorkflowNodeType.Selector),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "selector_1", "input", null),
                new ConnectionSchema("selector_1", "true", "exit_1", "input", "true")
            ]);

        var result = _validator.ValidateCanvas(JsonSerializer.Serialize(canvas));
        var codes = result.Errors.Select(e => e.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("SELECTOR_BRANCH_COUNT_INVALID", codes);
        Assert.Contains("SELECTOR_CONDITION_MISSING", codes);
        Assert.Contains("SELECTOR_FALSE_BRANCH_MISSING", codes);
    }

    [Fact]
    public void ValidateCanvas_ShouldReportInputMappingScopeViolation()
    {
        var nodeWithInvalidMapping = new NodeSchema(
            "transform_1",
            WorkflowNodeType.TextProcessor,
            "transform_1",
            new Dictionary<string, JsonElement>(),
            new NodeLayout(220, 0, 120, 60),
            InputSources:
            [
                new NodeFieldMapping("content", "future_1.value")
            ]);

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                nodeWithInvalidMapping,
                BuildNode("future_1", WorkflowNodeType.OutputEmitter)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "transform_1", "input", null),
                new ConnectionSchema("transform_1", "output", "future_1", "input", null)
            ]);

        var result = _validator.ValidateCanvas(JsonSerializer.Serialize(canvas));

        Assert.Contains(result.Errors, e => e.Code == "VARIABLE_MAPPING_SCOPE_INVALID");
    }

    [Fact]
    public void ValidateCanvas_ShouldReportMissingGlobalVariableMapping()
    {
        var nodeWithGlobalMapping = new NodeSchema(
            "transform_1",
            WorkflowNodeType.TextProcessor,
            "transform_1",
            new Dictionary<string, JsonElement>(),
            new NodeLayout(220, 0, 120, 60),
            InputSources:
            [
                new NodeFieldMapping("content", "global.missing_key")
            ]);

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                nodeWithGlobalMapping
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "transform_1", "input", null)
            ],
            Globals: new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["tenant_name"] = JsonSerializer.SerializeToElement("atlas")
            });

        var result = _validator.ValidateCanvas(JsonSerializer.Serialize(canvas));

        Assert.Contains(result.Errors, e => e.Code == "VARIABLE_MAPPING_GLOBAL_MISSING");
    }

    [Fact]
    public void ValidateCanvas_ShouldReportInvalidInputReceiverOutputSchema()
    {
        var inputReceiver = new NodeSchema(
            "input_1",
            WorkflowNodeType.InputReceiver,
            "input_1",
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["inputPath"] = JsonSerializer.SerializeToElement("input.message"),
                ["outputSchema"] = JsonSerializer.SerializeToElement("not-a-json-object")
            },
            new NodeLayout(220, 0, 120, 60));

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                inputReceiver
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "input_1", "input", null)
            ]);

        var result = _validator.ValidateCanvas(JsonSerializer.Serialize(canvas));

        Assert.Contains(result.Errors, e => e.Code == "INPUT_RECEIVER_OUTPUT_SCHEMA_INVALID");
    }

    [Fact]
    public void ValidateCanvas_ShouldReportConfigVariableReferenceScopeViolation()
    {
        var nodeWithRef = new NodeSchema(
            "processor_1",
            WorkflowNodeType.TextProcessor,
            "processor_1",
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = JsonSerializer.SerializeToElement("{{future_1.result}}")
            },
            new NodeLayout(220, 0, 120, 60));

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                nodeWithRef,
                BuildNode("future_1", WorkflowNodeType.OutputEmitter)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "processor_1", "input", null),
                new ConnectionSchema("processor_1", "output", "future_1", "input", null)
            ]);

        var result = _validator.ValidateCanvas(JsonSerializer.Serialize(canvas));

        Assert.Contains(result.Errors, e => e.Code == "VARIABLE_REFERENCE_SCOPE_INVALID");
    }

    [Fact]
    public void ValidateCanvas_ShouldAcceptEditorCanvasPayload()
    {
        const string json = """
            {
              "nodes": [
                {
                  "key": "entry_1",
                  "type": "Entry",
                  "title": "开始",
                  "configs": {
                    "entryVariable": "incident",
                    "entryAutoSaveHistory": true
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
                },
                {
                  "key": "exit_1",
                  "type": "Exit",
                  "title": "结束",
                  "configs": {
                    "exitTerminateMode": "return",
                    "exitTemplate": "{{result}}"
                  },
                  "layout": { "x": 660, "y": 80, "width": 160, "height": 60 }
                }
              ],
              "connections": [
                {
                  "fromNode": "entry_1",
                  "fromPort": "output",
                  "toNode": "text_1",
                  "toPort": "input",
                  "condition": null
                },
                {
                  "fromNode": "text_1",
                  "fromPort": "output",
                  "toNode": "exit_1",
                  "toPort": "input",
                  "condition": null
                }
              ]
            }
            """;

        var result = _validator.ValidateCanvas(json);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateCanvas_ShouldRejectCozeNativeCanvasPayload()
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
                          "input": {
                            "value": { "content": "{{code_1.result}}" }
                          }
                        }
                      ]
                    }
                  }
                }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "code_1" },
                { "sourceNodeID": "code_1", "targetNodeID": "exit_1" }
              ]
            }
            """;

        var result = _validator.ValidateCanvas(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "CANVAS_PARSE_FAILED");
    }

    private static NodeSchema BuildNode(string key, WorkflowNodeType nodeType)
    {
        return new NodeSchema(
            key,
            nodeType,
            key,
            new Dictionary<string, JsonElement>(),
            new NodeLayout(0, 0, 120, 60));
    }
}
