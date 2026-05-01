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

    private static JsonElement Case(object value)
        => JsonSerializer.SerializeToElement(value, JsonOptions);

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
