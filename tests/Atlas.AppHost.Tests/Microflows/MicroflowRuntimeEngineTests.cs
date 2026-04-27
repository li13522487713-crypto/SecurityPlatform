using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Services;
using Atlas.Domain.Microflows.Entities;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowRuntimeEngineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Run_StartToEnd_ReturnsNull()
    {
        var result = await RunAsync(Schema(Objects(Start(), End()), Flows(Flow("f1", "start", "end"))));

        Assert.Equal("success", result.Status);
        Assert.Equal(JsonValueKind.Null, result.Output?.ValueKind);
        Assert.Equal(["start", "end"], result.Trace.Select(frame => frame.ObjectId));
    }

    [Fact]
    public async Task Run_BindsNumberParameter_AndReturnsValue()
    {
        var result = await RunAsync(
            Schema(Objects(Start(), End(returnValue: "amount")), Flows(Flow("f1", "start", "end")), Parameters(Parameter("amount", "Number", required: true))),
            new Dictionary<string, object?> { ["amount"] = "150" });

        Assert.Equal("success", result.Status);
        Assert.Equal(150m, result.Output!.Value.GetDecimal());
    }

    [Fact]
    public async Task Run_RequiredParameterMissing_Fails()
    {
        var result = await RunAsync(
            Schema(Objects(Start(), End()), Flows(Flow("f1", "start", "end")), Parameters(Parameter("amount", "Number", required: true))));

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeVariableNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Run_InvalidParameterType_Fails()
    {
        var result = await RunAsync(
            Schema(Objects(Start(), End()), Flows(Flow("f1", "start", "end")), Parameters(Parameter("amount", "Number", required: true))),
            new Dictionary<string, object?> { ["amount"] = "abc" });

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeVariableTypeMismatch, result.Error?.Code);
    }

    [Fact]
    public async Task Run_CreateAndChangeVariable_ReturnsChangedValue()
    {
        var result = await RunAsync(Schema(
            Objects(
                Start(),
                Action("create", "createVariable", new { variableName = "approvalLevel", dataType = Type("string"), initialValue = "\"L1\"" }),
                Action("change", "changeVariable", new { targetVariableName = "approvalLevel", newValueExpression = "\"L2\"" }),
                End(returnValue: "approvalLevel")),
            Flows(
                Flow("f1", "start", "create"),
                Flow("f2", "create", "change"),
                Flow("f3", "change", "end"))));

        Assert.Equal("success", result.Status);
        Assert.Equal("L2", result.Output?.GetString());
        Assert.Contains(result.Trace, frame => frame.ObjectId == "create" && frame.Status == "success");
        Assert.Contains(result.Trace, frame => frame.ObjectId == "change" && frame.Status == "success");
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(50, false)]
    public async Task Run_Decision_ChoosesBooleanBranch(int amount, bool expected)
    {
        var result = await RunAsync(
            Schema(
                Objects(Start(), Decision("decision", "amount > 100"), End("trueEnd", "true"), End("falseEnd", "false")),
                Flows(
                    Flow("f1", "start", "decision"),
                    Flow("fTrue", "decision", "trueEnd", true),
                    Flow("fFalse", "decision", "falseEnd", false)),
                Parameters(Parameter("amount", "Number", required: true))),
            new Dictionary<string, object?> { ["amount"] = amount });

        Assert.Equal("success", result.Status);
        Assert.Equal(expected, result.Output?.GetBoolean());
        Assert.Contains(result.Trace, frame => frame.ObjectId == "decision" && frame.OutgoingFlowId == (expected ? "fTrue" : "fFalse"));
    }

    [Fact]
    public async Task Run_UnsupportedExpression_Fails()
    {
        var result = await RunAsync(
            Schema(
                Objects(Start(), Decision("decision", "unsupported(amount)"), End()),
                Flows(Flow("f1", "start", "decision"), Flow("fTrue", "decision", "end", true)),
                Parameters(Parameter("amount", "Number", required: true))),
            new Dictionary<string, object?> { ["amount"] = 150 });

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeExpressionError, result.Error?.Code);
    }

    [Fact]
    public async Task Run_Merge_PassesThroughToEnd()
    {
        var result = await RunAsync(Schema(
            Objects(Start(), new { id = "merge", kind = "exclusiveMerge", caption = "Merge" }, End()),
            Flows(Flow("f1", "start", "merge"), Flow("f2", "merge", "end"))));

        Assert.Equal("success", result.Status);
        Assert.Contains(result.Trace, frame => frame.ObjectId == "merge" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_EndReturnExpression_ReturnsVariable()
    {
        var result = await RunAsync(Schema(
            Objects(
                Start(),
                Action("create", "createVariable", new { variableName = "result", dataType = Type("integer"), initialValue = "1 + 1" }),
                End(returnValue: "result")),
            Flows(Flow("f1", "start", "create"), Flow("f2", "create", "end"))));

        Assert.Equal("success", result.Status);
        Assert.Equal(2m, result.Output!.Value.GetDecimal());
    }

    [Fact]
    public async Task Run_UnsupportedNode_Fails()
    {
        var result = await RunAsync(Schema(
            Objects(Start(), Action("rest", "restCall", new { request = new { method = "GET" } }), End()),
            Flows(Flow("f1", "start", "rest"), Flow("f2", "rest", "end"))));

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeUnsupportedAction, result.Error?.Code);
        Assert.Contains("restCall", result.Error?.Message);
    }

    [Fact]
    public async Task Run_MissingStart_Fails()
    {
        var result = await RunAsync(Schema(Objects(End()), Flows()));

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeStartNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Run_DanglingFlow_Fails()
    {
        var result = await RunAsync(Schema(Objects(Start(), End()), Flows(Flow("f1", "start", "missing"))));

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeFlowNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Run_MaxStepCount_PreventsInfiniteLoop()
    {
        var result = await RunAsync(
            Schema(Objects(Start()), Flows(Flow("f1", "start", "start"))),
            options: new MicroflowTestRunOptionsDto { MaxSteps = 2 });

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeMaxStepsExceeded, result.Error?.Code);
    }

    [Fact]
    public async Task Run_NodeResults_ContainExecutedPath()
    {
        var result = await RunAsync(Schema(Objects(Start(), End()), Flows(Flow("f1", "start", "end"))));

        Assert.Equal(["start", "end"], result.Trace.Select(frame => frame.ObjectId));
        Assert.All(result.Trace, frame => Assert.Equal("success", frame.Status));
    }

    [Fact]
    public async Task Run_Contexts_DoNotShareVariables()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("create", "createVariable", new { variableName = "value", dataType = Type("string"), initialValue = "inputValue" }),
                End(returnValue: "value")),
            Flows(Flow("f1", "start", "create"), Flow("f2", "create", "end")),
            Parameters(Parameter("inputValue", "String", required: true)));

        var first = await RunAsync(schema, new Dictionary<string, object?> { ["inputValue"] = "A" });
        var second = await RunAsync(schema, new Dictionary<string, object?> { ["inputValue"] = "B" });

        Assert.Equal("A", first.Output?.GetString());
        Assert.Equal("B", second.Output?.GetString());
    }

    [Fact]
    public async Task Run_CallMicroflow_ExecutesChildAndBindsReturnValue()
    {
        const string childResourceId = "mf-child";
        const string childSnapshotId = "snapshot-child";
        var (resourceRepository, snapshotRepository) = BuildCallTargetRepositories(
            childResourceId,
            childSnapshotId,
            ChildValidationSchema(childResourceId));
        var parent = ParentCallSchema("mf-parent", childResourceId, includeMapping: true, callMode: "sync");

        var result = await RunAsync(
            parent,
            new Dictionary<string, object?> { ["totalAmount"] = 150 },
            resourceRepository: resourceRepository,
            snapshotRepository: snapshotRepository);

        Assert.Equal("success", result.Status);
        Assert.Equal("High", result.Output?.GetString());
        Assert.Single(result.ChildRuns);
        Assert.Equal("success", result.ChildRuns[0].Status);
        Assert.Contains(result.Trace, frame => frame.ObjectId == "call" && frame.MicroflowId == "mf-parent");
        Assert.Contains(result.ChildRuns[0].Trace, frame => frame.ObjectId == "decision" && frame.MicroflowId == childResourceId);
    }

    [Fact]
    public async Task Run_CallMicroflow_RequiredParameterMappingMissing_Fails()
    {
        const string childResourceId = "mf-child";
        const string childSnapshotId = "snapshot-child";
        var (resourceRepository, snapshotRepository) = BuildCallTargetRepositories(
            childResourceId,
            childSnapshotId,
            ChildValidationSchema(childResourceId));
        var parent = ParentCallSchema("mf-parent", childResourceId, includeMapping: false, callMode: "sync");

        var result = await RunAsync(
            parent,
            new Dictionary<string, object?> { ["totalAmount"] = 150 },
            resourceRepository: resourceRepository,
            snapshotRepository: snapshotRepository);

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeParameterMappingMissing, result.Error?.Code);
    }

    [Fact]
    public async Task Run_CallMicroflow_UnsupportedCallMode_Fails()
    {
        const string childResourceId = "mf-child";
        const string childSnapshotId = "snapshot-child";
        var (resourceRepository, snapshotRepository) = BuildCallTargetRepositories(
            childResourceId,
            childSnapshotId,
            ChildValidationSchema(childResourceId));
        var parent = ParentCallSchema("mf-parent", childResourceId, includeMapping: true, callMode: "asyncReserved");

        var result = await RunAsync(
            parent,
            new Dictionary<string, object?> { ["totalAmount"] = 150 },
            resourceRepository: resourceRepository,
            snapshotRepository: snapshotRepository);

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeUnsupportedCallMode, result.Error?.Code);
    }

    [Fact]
    public async Task Run_CallMicroflow_TargetNotFound_Fails()
    {
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        resourceRepository.GetByIdAsync("missing-target", Arg.Any<CancellationToken>())
            .Returns((MicroflowResourceEntity?)null);
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var parent = ParentCallSchema("mf-parent", "missing-target", includeMapping: true, callMode: "sync");

        var result = await RunAsync(
            parent,
            new Dictionary<string, object?> { ["totalAmount"] = 150 },
            resourceRepository: resourceRepository,
            snapshotRepository: snapshotRepository);

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeTargetMicroflowNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Run_CallMicroflow_DirectSelfCall_Fails()
    {
        var parent = ParentCallSchema("mf-parent", "mf-parent", includeMapping: true, callMode: "sync");

        var result = await RunAsync(parent, new Dictionary<string, object?> { ["totalAmount"] = 150 });

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeCallRecursionDetected, result.Error?.Code);
    }

    [Fact]
    public async Task Run_CallMicroflow_CycleDetection_AtoBtoA_Fails()
    {
        const string resourceA = "mf-a";
        const string resourceB = "mf-b";
        const string snapshotA = "snapshot-a";
        const string snapshotB = "snapshot-b";
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        resourceRepository.GetByIdAsync(resourceA, Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity { Id = resourceA, Version = "1", CurrentSchemaSnapshotId = snapshotA });
        resourceRepository.GetByIdAsync(resourceB, Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity { Id = resourceB, Version = "1", CurrentSchemaSnapshotId = snapshotB });
        snapshotRepository.GetByIdAsync(snapshotA, Arg.Any<CancellationToken>())
            .Returns(new MicroflowSchemaSnapshotEntity { Id = snapshotA, ResourceId = resourceA, SchemaJson = CallOnlySchema(resourceA, resourceB).GetRawText() });
        snapshotRepository.GetByIdAsync(snapshotB, Arg.Any<CancellationToken>())
            .Returns(new MicroflowSchemaSnapshotEntity { Id = snapshotB, ResourceId = resourceB, SchemaJson = CallOnlySchema(resourceB, resourceA).GetRawText() });

        var result = await RunAsync(
            CallOnlySchema(resourceA, resourceB),
            resourceRepository: resourceRepository,
            snapshotRepository: snapshotRepository);

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeCallRecursionDetected, result.Error?.Code);
        Assert.NotNull(result.Error?.CallStack);
    }

    [Fact]
    public async Task Run_CallMicroflow_MaxDepthExceeded_Fails()
    {
        const string childResourceId = "mf-child";
        const string childSnapshotId = "snapshot-child";
        const string grandResourceId = "mf-grand";
        const string grandSnapshotId = "snapshot-grand";
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        resourceRepository.GetByIdAsync(childResourceId, Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity { Id = childResourceId, Version = "1", CurrentSchemaSnapshotId = childSnapshotId });
        resourceRepository.GetByIdAsync(grandResourceId, Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity { Id = grandResourceId, Version = "1", CurrentSchemaSnapshotId = grandSnapshotId });
        snapshotRepository.GetByIdAsync(childSnapshotId, Arg.Any<CancellationToken>())
            .Returns(new MicroflowSchemaSnapshotEntity
            {
                Id = childSnapshotId,
                ResourceId = childResourceId,
                SchemaJson = CallOnlySchema(childResourceId, grandResourceId).GetRawText()
            });
        snapshotRepository.GetByIdAsync(grandSnapshotId, Arg.Any<CancellationToken>())
            .Returns(new MicroflowSchemaSnapshotEntity
            {
                Id = grandSnapshotId,
                ResourceId = grandResourceId,
                SchemaJson = Schema(Objects(Start(), End()), Flows(Flow("f1", "start", "end")), id: grandResourceId).GetRawText()
            });
        var parent = CallOnlySchema("mf-parent", childResourceId);

        var result = await RunAsync(
            parent,
            options: new MicroflowTestRunOptionsDto(),
            maxCallDepth: 1,
            resourceRepository: resourceRepository,
            snapshotRepository: snapshotRepository);

        Assert.Equal("failed", result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeCallStackOverflow, result.Error?.Code);
    }

    private static Task<MicroflowRunSessionDto> RunAsync(
        JsonElement schema,
        IReadOnlyDictionary<string, object?>? input = null,
        MicroflowTestRunOptionsDto? options = null,
        int maxCallDepth = 10,
        IMicroflowResourceRepository? resourceRepository = null,
        IMicroflowSchemaSnapshotRepository? snapshotRepository = null)
    {
        var engine = new MicroflowRuntimeEngine(new MicroflowSchemaReader(), new FixedClock(), resourceRepository, snapshotRepository);
        var jsonInput = (input ?? new Dictionary<string, object?>())
            .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions), StringComparer.Ordinal);
        var schemaId = schema.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString() ?? "mf-test"
            : "mf-test";
        return engine.RunAsync(
            new MicroflowMockRuntimeRequest
            {
                ResourceId = schemaId,
                SchemaId = "schema-test",
                Version = "1",
                Schema = schema,
                Input = jsonInput,
                Options = options ?? new MicroflowTestRunOptionsDto(),
                RequestContext = new MicroflowRequestContext { TraceId = Guid.NewGuid().ToString("N") },
                MaxCallDepth = maxCallDepth
            },
            CancellationToken.None);
    }

    private static (IMicroflowResourceRepository ResourceRepository, IMicroflowSchemaSnapshotRepository SnapshotRepository) BuildCallTargetRepositories(
        string targetResourceId,
        string targetSnapshotId,
        JsonElement targetSchema)
    {
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        resourceRepository.GetByIdAsync(targetResourceId, Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity
            {
                Id = targetResourceId,
                Version = "1",
                CurrentSchemaSnapshotId = targetSnapshotId
            });
        snapshotRepository.GetByIdAsync(targetSnapshotId, Arg.Any<CancellationToken>())
            .Returns(new MicroflowSchemaSnapshotEntity
            {
                Id = targetSnapshotId,
                ResourceId = targetResourceId,
                SchemaJson = targetSchema.GetRawText()
            });

        return (resourceRepository, snapshotRepository);
    }

    private static JsonElement ParentCallSchema(string resourceId, string targetMicroflowId, bool includeMapping, string callMode)
    {
        var mappings = includeMapping
            ? new[] { new { parameterName = "amount", argumentExpression = "totalAmount" } }
            : Array.Empty<object>();
        return Schema(
            Objects(
                Start(),
                Action("call", "callMicroflow", new
                {
                    targetMicroflowId,
                    targetMicroflowQualifiedName = $"Module.{targetMicroflowId}",
                    parameterMappings = mappings,
                    returnValue = new { storeResult = true, outputVariableName = "validationResult" },
                    callMode
                }),
                End(returnValue: "validationResult")),
            Flows(
                Flow("f1", "start", "call"),
                Flow("f2", "call", "end")),
            Parameters(Parameter("totalAmount", "Number", required: true)),
            id: resourceId);
    }

    private static JsonElement ChildValidationSchema(string resourceId)
        => Schema(
            Objects(Start(), Decision("decision", "amount > 100"), End("high", "\"High\""), End("normal", "\"Normal\"")),
            Flows(
                Flow("f1", "start", "decision"),
                Flow("fTrue", "decision", "high", true),
                Flow("fFalse", "decision", "normal", false)),
            Parameters(Parameter("amount", "Number", required: true)),
            id: resourceId);

    private static JsonElement CallOnlySchema(string resourceId, string targetMicroflowId)
        => Schema(
            Objects(
                Start(),
                Action("call", "callMicroflow", new
                {
                    targetMicroflowId,
                    targetMicroflowQualifiedName = $"Module.{targetMicroflowId}",
                    parameterMappings = Array.Empty<object>(),
                    returnValue = new { storeResult = false },
                    callMode = "sync"
                }),
                End()),
            Flows(
                Flow("f1", "start", "call"),
                Flow("f2", "call", "end")),
            id: resourceId);

    private static JsonElement Schema(IReadOnlyList<object> objects, IReadOnlyList<object> flows, IReadOnlyList<object>? parameters = null, string id = "mf-test")
        => JsonSerializer.SerializeToElement(new
        {
            schemaVersion = "1.0.0",
            id,
            name = id,
            displayName = id,
            moduleId = "mod",
            parameters = parameters ?? [],
            returnType = Type("unknown"),
            objectCollection = new { id = "root", objects },
            flows,
            security = new { },
            concurrency = new { },
            exposure = new { },
            validation = new { },
            editor = new { },
            audit = new { }
        }, JsonOptions);

    private static IReadOnlyList<object> Objects(params object[] objects) => objects;

    private static IReadOnlyList<object> Flows(params object[] flows) => flows;

    private static IReadOnlyList<object> Parameters(params object[] parameters) => parameters;

    private static object Type(string kind) => new { kind };

    private static object Parameter(string name, string dataType, bool required)
        => new { id = name, name, dataType, required };

    private static object Start(string id = "start")
        => new { id, kind = "startEvent", caption = "Start" };

    private static object End(string id = "end", string? returnValue = null)
        => returnValue is null
            ? new { id, kind = "endEvent", caption = "End" }
            : new { id, kind = "endEvent", caption = "End", returnValue };

    private static object Decision(string id, string expression)
        => new { id, kind = "exclusiveSplit", caption = "Decision", splitCondition = new { expression, resultType = "boolean" } };

    private static object Action(string id, string actionKind, object action)
        => new { id, kind = "actionActivity", caption = id, action = MergeAction(id, actionKind, action) };

    private static object MergeAction(string id, string actionKind, object action)
    {
        var json = JsonSerializer.SerializeToElement(action, JsonOptions);
        var values = new Dictionary<string, object?>
        {
            ["id"] = $"{id}-action",
            ["kind"] = actionKind,
            ["officialType"] = $"Microflows${actionKind}"
        };
        foreach (var property in json.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return values;
    }

    private static object Flow(string id, string source, string target, bool? booleanCase = null)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = booleanCase is null ? [] : new[] { new { kind = "boolean", value = booleanCase.Value, persistedValue = booleanCase.Value ? "true" : "false" } },
            isErrorHandler = false,
            editor = new { edgeKind = booleanCase is null ? "sequence" : "decisionCondition" }
        };

    private sealed class FixedClock : IMicroflowClock
    {
        private DateTimeOffset _now = new(2026, 4, 28, 0, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow
        {
            get
            {
                _now = _now.AddMilliseconds(1);
                return _now;
            }
        }
    }
}
