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

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
