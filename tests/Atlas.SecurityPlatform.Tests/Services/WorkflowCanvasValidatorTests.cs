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
