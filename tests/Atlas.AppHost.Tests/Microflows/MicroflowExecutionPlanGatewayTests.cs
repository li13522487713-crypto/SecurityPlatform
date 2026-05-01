using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowExecutionPlanGatewayTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Build_Compiles_ParallelGateway_Split_And_Merge_Descriptors()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new { id = "fork", kind = "parallelGateway", caption = "Fork" },
                new { id = "left", kind = "actionActivity", caption = "Left", action = new { id = "left-action", kind = "createVariable", variableName = "leftValue", initialValue = "1" } },
                new { id = "right", kind = "actionActivity", caption = "Right", action = new { id = "right-action", kind = "createVariable", variableName = "rightValue", initialValue = "2" } },
                new { id = "join", kind = "parallelGateway", caption = "Join" },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "fork"),
                Flow("f2", "fork", "left"),
                Flow("f3", "fork", "right"),
                Flow("f4", "left", "join"),
                Flow("f5", "right", "join"),
                Flow("f6", "join", "end")
            ],
            parameters: null,
            id: "mf-gateway-plan",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);
        var plan = new MicroflowExecutionPlanBuilder(new MicroflowExecutionPlanValidator(), new TestClock())
            .Build(runtimeDto, options);

        var fork = Assert.Single(plan.Gateways, gateway => gateway.ObjectId == "fork");
        Assert.Equal("parallelGateway", fork.Kind);
        Assert.Equal("split", fork.Role);
        Assert.Equal(["f1"], fork.IncomingFlowIds);
        Assert.Equal(["f2", "f3"], fork.OutgoingFlowIds);
        Assert.Equal(["f2", "f3"], fork.BranchFlowIds);

        var join = Assert.Single(plan.Gateways, gateway => gateway.ObjectId == "join");
        Assert.Equal("merge", join.Role);
        Assert.Equal(["f4", "f5"], join.IncomingFlowIds);
        Assert.Equal(["f6"], join.OutgoingFlowIds);
    }

    [Fact]
    public void Build_Allows_InclusiveGateway_Conditional_Branch_Flows()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new { id = "fork", kind = "inclusiveGateway", caption = "Fork" },
                new { id = "left", kind = "actionActivity", caption = "Left", action = new { id = "left-action", kind = "createVariable", variableName = "leftValue", dataType = new { kind = "integer" }, initialValue = "1" } },
                new { id = "right", kind = "actionActivity", caption = "Right", action = new { id = "right-action", kind = "createVariable", variableName = "rightValue", dataType = new { kind = "integer" }, initialValue = "2" } },
                new { id = "join", kind = "inclusiveGateway", caption = "Join" },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "fork"),
                ConditionFlow("f2", "fork", "left", "1 = 1"),
                ConditionFlow("f3", "fork", "right", "2 = 2"),
                Flow("f4", "left", "join"),
                Flow("f5", "right", "join"),
                Flow("f6", "join", "end")
            ],
            parameters: null,
            id: "mf-inclusive-gateway-plan",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);
        var plan = new MicroflowExecutionPlanBuilder(new MicroflowExecutionPlanValidator(), new TestClock())
            .Build(runtimeDto, options);

        Assert.Equal(0, plan.Validation.ErrorCount);
        Assert.DoesNotContain(plan.Validation.Diagnostics, issue => issue.Code == "RUNTIME_DECISION_FLOW_SOURCE_INVALID");
        Assert.DoesNotContain(plan.Validation.Diagnostics, issue => issue.Code == "RUNTIME_NODE_DEAD_END" && issue.ObjectId == "fork");
        var fork = Assert.Single(plan.Gateways, gateway => gateway.ObjectId == "fork");
        Assert.Equal("split", fork.Role);
        Assert.Equal(["f2", "f3"], fork.BranchFlowIds);
    }

    [Fact]
    public void Validate_Rejects_Invalid_Gateway_Descriptors()
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-invalid-gateway",
            SchemaId = "schema-invalid-gateway",
            StartNodeId = "start",
            EndNodeIds = ["end"],
            Nodes =
            [
                new MicroflowExecutionNode { ObjectId = "start", Kind = "startEvent", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "fork", Kind = "parallelGateway", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "end", Kind = "endEvent", RuntimeBehavior = "executable" }
            ],
            Flows =
            [
                new MicroflowExecutionFlow { FlowId = "f1", ControlFlow = "normal", OriginObjectId = "start", DestinationObjectId = "fork" },
                new MicroflowExecutionFlow { FlowId = "f2", ControlFlow = "normal", OriginObjectId = "fork", DestinationObjectId = "end" }
            ],
            NormalFlows =
            [
                new MicroflowExecutionFlow { FlowId = "f1", ControlFlow = "normal", OriginObjectId = "start", DestinationObjectId = "fork" },
                new MicroflowExecutionFlow { FlowId = "f2", ControlFlow = "normal", OriginObjectId = "fork", DestinationObjectId = "end" }
            ],
            Gateways =
            [
                new MicroflowExecutionGateway
                {
                    ObjectId = "fork",
                    Kind = "parallelGateway",
                    Role = "split",
                    IncomingFlowIds = ["f1"],
                    OutgoingFlowIds = ["f2", "missing-flow"],
                    BranchFlowIds = ["f2"]
                },
                new MicroflowExecutionGateway
                {
                    ObjectId = "missing-gateway",
                    Kind = "parallelGateway",
                    Role = "merge",
                    IncomingFlowIds = [],
                    OutgoingFlowIds = []
                }
            ]
        };

        var result = new MicroflowExecutionPlanValidator()
            .Validate(plan, new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun });

        Assert.Contains(result.Diagnostics, issue => issue.Code == "RUNTIME_GATEWAY_FLOW_NOT_FOUND" && issue.FlowId == "missing-flow");
        Assert.Contains(result.Diagnostics, issue => issue.Code == "RUNTIME_GATEWAY_SPLIT_BRANCH_MISSING" && issue.ObjectId == "fork");
        Assert.Contains(result.Diagnostics, issue => issue.Code == "RUNTIME_GATEWAY_NODE_NOT_FOUND" && issue.ObjectId == "missing-gateway");
    }

    private static object Flow(string id, string source, string target)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = Array.Empty<object>(),
            isErrorHandler = false,
            editor = new { edgeKind = "sequence" }
        };

    private static object ConditionFlow(string id, string source, string target, string expression)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = new[] { new { kind = "expression", condition = expression, expression } },
            isErrorHandler = false,
            editor = new { edgeKind = "decisionCondition" }
        };

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
