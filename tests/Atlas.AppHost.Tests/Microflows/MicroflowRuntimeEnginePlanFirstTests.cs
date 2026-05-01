using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowRuntimeEnginePlanFirstTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RunAsync_Uses_Provided_ExecutionPlan_Before_Schema_Graph()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                new
                {
                    id = "broken-flow",
                    originObjectId = "start",
                    destinationObjectId = "missing",
                    kind = "sequence",
                    caseValues = Array.Empty<object>()
                }
            ],
            parameters: null,
            id: "mf-plan-first",
            options: JsonOptions);
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-mf-plan-first",
            ResourceId = "mf-plan-first",
            SchemaId = "mf-plan-first",
            Version = "v1",
            StartNodeId = "start",
            EndNodeIds = ["end"],
            Nodes =
            [
                new MicroflowExecutionNode { ObjectId = "start", Kind = "startEvent", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "end", Kind = "endEvent", RuntimeBehavior = "executable" }
            ],
            Flows =
            [
                new MicroflowExecutionFlow
                {
                    FlowId = "plan-flow",
                    ControlFlow = "normal",
                    OriginObjectId = "start",
                    DestinationObjectId = "end"
                }
            ],
            NormalFlows =
            [
                new MicroflowExecutionFlow
                {
                    FlowId = "plan-flow",
                    ControlFlow = "normal",
                    OriginObjectId = "start",
                    DestinationObjectId = "end"
                }
            ]
        };
        var engine = new MicroflowRuntimeEngine(new MicroflowSchemaReader(), new TestClock());

        var session = await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-plan-first",
                SchemaId = "schema-plan-first",
                Version = "v1",
                Schema = schema,
                ExecutionPlan = plan,
                ExecutionMode = MicroflowRuntimeExecutionMode.TestRun,
                RequestContext = new MicroflowRequestContext { TraceId = "trace-plan-first" }
            },
            CancellationToken.None);

        Assert.Equal("success", session.Status);
        Assert.Equal(["start", "end"], session.Trace.Select(frame => frame.ObjectId));
    }

    [Fact]
    public async Task RunAsync_Uses_Plan_DecisionFlows_As_Runtime_Outgoing_Flows()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new { id = "decision", kind = "exclusiveSplit", caption = "Decision", splitCondition = new { expression = "true" } },
                new { id = "end-true", kind = "endEvent", caption = "True End", returnValue = "120" },
                new { id = "end-false", kind = "endEvent", caption = "False End", returnValue = "0" }
            ],
            flows:
            [
                new { id = "start-decision", originObjectId = "start", destinationObjectId = "decision", kind = "sequence", caseValues = Array.Empty<object>() },
                new { id = "decision-true", originObjectId = "decision", destinationObjectId = "end-true", kind = "sequence", caseValues = Array.Empty<object>() },
                new { id = "decision-false", originObjectId = "decision", destinationObjectId = "end-false", kind = "sequence", caseValues = Array.Empty<object>() }
            ],
            parameters: null,
            id: "mf-plan-decision",
            options: JsonOptions);
        var startFlow = new MicroflowExecutionFlow
        {
            FlowId = "start-decision",
            ControlFlow = "normal",
            OriginObjectId = "start",
            DestinationObjectId = "decision"
        };
        var trueFlow = new MicroflowExecutionFlow
        {
            FlowId = "decision-true",
            EdgeKind = "decisionCondition",
            ControlFlow = "decision",
            OriginObjectId = "decision",
            DestinationObjectId = "end-true",
            CaseValues = [Case(new { kind = "boolean", value = true, persistedValue = "true" })]
        };
        var falseFlow = new MicroflowExecutionFlow
        {
            FlowId = "decision-false",
            EdgeKind = "decisionCondition",
            ControlFlow = "decision",
            OriginObjectId = "decision",
            DestinationObjectId = "end-false",
            CaseValues = [Case(new { kind = "boolean", value = false, persistedValue = "false" })]
        };
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-mf-plan-decision",
            ResourceId = "mf-plan-decision",
            SchemaId = "mf-plan-decision",
            Version = "v1",
            StartNodeId = "start",
            EndNodeIds = ["end-true", "end-false"],
            Nodes =
            [
                new MicroflowExecutionNode { ObjectId = "start", Kind = "startEvent", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "decision", Kind = "exclusiveSplit", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "end-true", Kind = "endEvent", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "end-false", Kind = "endEvent", RuntimeBehavior = "executable" }
            ],
            Flows = [startFlow, trueFlow, falseFlow],
            NormalFlows = [startFlow],
            DecisionFlows = [trueFlow, falseFlow]
        };
        var engine = new MicroflowRuntimeEngine(new MicroflowSchemaReader(), new TestClock());

        var session = await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-plan-decision",
                SchemaId = "schema-plan-decision",
                Version = "v1",
                Schema = schema,
                ExecutionPlan = plan,
                ExecutionMode = MicroflowRuntimeExecutionMode.TestRun,
                RequestContext = new MicroflowRequestContext { TraceId = "trace-plan-decision" }
            },
            CancellationToken.None);

        Assert.Equal("success", session.Status);
        Assert.Equal("120", session.Output?.GetRawText());
        Assert.Contains(session.Trace, frame => frame.ObjectId == "decision" && frame.OutgoingFlowId == "decision-true");
    }

    [Fact]
    public async Task RunAsync_Uses_Plan_Gateway_Merge_Before_Distance_Based_Graph_Fallback()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new { id = "fork", kind = "parallelGateway", caption = "Fork" },
                new { id = "left", kind = "actionActivity", caption = "Left", action = new { id = "left-action", kind = "createVariable", variableName = "leftValue", dataType = new { kind = "integer" }, initialValue = "1" } },
                new { id = "right", kind = "actionActivity", caption = "Right", action = new { id = "right-action", kind = "createVariable", variableName = "rightValue", dataType = new { kind = "integer" }, initialValue = "2" } },
                new { id = "nearest-join", kind = "parallelGateway", caption = "Nearest graph join" },
                new { id = "planned-join", kind = "parallelGateway", caption = "Planned join" },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows: Array.Empty<object>(),
            parameters: null,
            id: "mf-plan-gateway",
            options: JsonOptions);
        var flows = new[]
        {
            PlanFlow("f1", "start", "fork"),
            PlanFlow("f2", "fork", "left"),
            PlanFlow("f3", "fork", "right"),
            PlanFlow("f4", "left", "nearest-join"),
            PlanFlow("f5", "right", "nearest-join"),
            PlanFlow("f6", "nearest-join", "planned-join"),
            PlanFlow("f7", "planned-join", "end")
        };
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-mf-plan-gateway",
            ResourceId = "mf-plan-gateway",
            SchemaId = "mf-plan-gateway",
            Version = "v1",
            StartNodeId = "start",
            EndNodeIds = ["end"],
            Nodes =
            [
                new MicroflowExecutionNode { ObjectId = "start", Kind = "startEvent", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "fork", Kind = "parallelGateway", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "left", Kind = "actionActivity", ActionKind = "createVariable", RuntimeBehavior = "executable", ConfigJson = ActionConfig("leftValue", "1") },
                new MicroflowExecutionNode { ObjectId = "right", Kind = "actionActivity", ActionKind = "createVariable", RuntimeBehavior = "executable", ConfigJson = ActionConfig("rightValue", "2") },
                new MicroflowExecutionNode { ObjectId = "nearest-join", Kind = "parallelGateway", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "planned-join", Kind = "parallelGateway", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "end", Kind = "endEvent", RuntimeBehavior = "executable" }
            ],
            Flows = flows,
            NormalFlows = flows,
            Gateways =
            [
                new MicroflowExecutionGateway { ObjectId = "fork", Kind = "parallelGateway", Role = "split", IncomingFlowIds = ["f1"], OutgoingFlowIds = ["f2", "f3"], BranchFlowIds = ["f2", "f3"] },
                new MicroflowExecutionGateway { ObjectId = "nearest-join", Kind = "parallelGateway", Role = "passThrough", IncomingFlowIds = ["f4", "f5"], OutgoingFlowIds = ["f6"] },
                new MicroflowExecutionGateway { ObjectId = "planned-join", Kind = "parallelGateway", Role = "merge", IncomingFlowIds = ["f6"], OutgoingFlowIds = ["f7"] }
            ]
        };
        var engine = new MicroflowRuntimeEngine(new MicroflowSchemaReader(), new TestClock());

        var session = await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-plan-gateway",
                SchemaId = "schema-plan-gateway",
                Version = "v1",
                Schema = schema,
                ExecutionPlan = plan,
                ExecutionMode = MicroflowRuntimeExecutionMode.TestRun,
                RequestContext = new MicroflowRequestContext { TraceId = "trace-plan-gateway" }
            },
            CancellationToken.None);

        Assert.Equal("success", session.Status);
        var forkFrame = Assert.Single(session.Trace, frame => frame.ObjectId == "fork");
        Assert.True(forkFrame.Output.HasValue);
        Assert.Equal("planned-join", forkFrame.Output.Value.GetProperty("joinNodeId").GetString());
    }

    private static JsonElement Case(object value)
        => JsonSerializer.SerializeToElement(value, JsonOptions);

    private static MicroflowExecutionFlow PlanFlow(string id, string source, string target)
        => new()
        {
            FlowId = id,
            ControlFlow = "normal",
            OriginObjectId = source,
            DestinationObjectId = target
        };

    private static JsonElement ActionConfig(string variableName, string initialValue)
        => JsonSerializer.SerializeToElement(new
        {
            id = $"{variableName}-action",
            kind = "createVariable",
            variableName,
            dataType = new { kind = "integer" },
            initialValue
        }, JsonOptions);

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
