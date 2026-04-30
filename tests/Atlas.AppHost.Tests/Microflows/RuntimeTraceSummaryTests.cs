using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class RuntimeTraceSummaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishedRun_Trace_Does_Not_Persist_Raw_Variable_Value()
    {
        var payload = new string('x', 8192);
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "create",
                    kind = "actionActivity",
                    caption = "Create",
                    action = new
                    {
                        id = "create-action",
                        kind = "createVariable",
                        variableName = "payload",
                        dataType = new { kind = "string" },
                        initialValue = $"\"{payload}\""
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End", returnValue = "payload" }
            ],
            flows:
            [
                new { id = "f1", originObjectId = "start", destinationObjectId = "create", kind = "sequence", caseValues = Array.Empty<object>() },
                new { id = "f2", originObjectId = "create", destinationObjectId = "end", kind = "sequence", caseValues = Array.Empty<object>() }
            ],
            parameters: null,
            id: "mf-trace-summary",
            options: JsonOptions);
        var engine = new MicroflowRuntimeEngine(new MicroflowSchemaReader(), new TestClock());

        var session = await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-trace-summary",
                SchemaId = "schema-trace-summary",
                Version = "v1",
                Schema = schema,
                ExecutionMode = MicroflowRuntimeExecutionMode.PublishedRun,
                RequestContext = new MicroflowRequestContext { TraceId = "trace-summary" }
            },
            CancellationToken.None);

        Assert.Equal("success", session.Status);
        var payloadSnapshot = session.Trace
            .SelectMany(frame => frame.VariablesSnapshot?.Values ?? Array.Empty<MicroflowRuntimeVariableValueDto>())
            .FirstOrDefault(variable => variable.Name == "payload");
        Assert.NotNull(payloadSnapshot);
        Assert.Null(payloadSnapshot!.RawValueJson);
    }

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
