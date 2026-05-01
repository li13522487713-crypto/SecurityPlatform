using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Actions.Http;
using Atlas.Application.Microflows.Runtime.Debug;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Security;
using Atlas.Application.Microflows.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// 这里覆盖 stage 25 引入的 Registry 派发路径：当 Engine 拿到
/// IMicroflowActionExecutorRegistry / IMicroflowRuntimeConnectorRegistry 时，
/// 应当把非 fast-path 动作（retrieve / createObject / restCall / logMessage /
/// aggregateList ...) 通过具体 Executor 真正执行；不允许假成功，也不允许
/// 把 supported 动作仍然返回 RUNTIME_UNSUPPORTED_ACTION。
/// </summary>
public sealed class MicroflowRuntimeEngineRegistryDispatchTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Run_LogMessageAction_ExecutesViaRegistryAndProducesLog()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("log", "logMessage", new
                {
                    level = "info",
                    template = new { text = "Hello {0}", arguments = new[] { new { expression = "$message" } } },
                    includeTraceId = true,
                    includeContextVariables = false,
                    logNodeName = "TestNode"
                }),
                End(returnValue: "$message")),
            Flows(
                Flow("f1", "start", "log"),
                Flow("f2", "log", "end")),
            Parameters(Parameter("message", "String", required: true)));

        var session = await RunWithRegistryAsync(schema, new Dictionary<string, object?> { ["message"] = "world" });

        Assert.Equal("success", session.Status);
        Assert.Equal("world", session.Output?.GetString());
        Assert.Single(session.Logs);
        Assert.Equal("info", session.Logs[0].Level);
        Assert.Contains("Hello world", session.Logs[0].Message);
    }

    [Fact]
    public async Task Run_RestCallAction_BlockedByDefaultSecurityPolicy()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("rest", "restCall", new
                {
                    method = "GET",
                    url = "https://example.com/api/test",
                    request = new { method = "GET", url = "https://example.com/api/test" }
                }),
                End()),
            Flows(
                Flow("f1", "start", "rest"),
                Flow("f2", "rest", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.NotNull(session.Error);
        // Default security policy blocks real HTTP unless allowRealHttp is set.
        Assert.False(session.Error!.Code is null || session.Error!.Code == RuntimeErrorCode.RuntimeUnknownError);
    }

    [Fact]
    public async Task Run_RestCallAction_WithDebugSession_EmitsInternalSafePointsWhenSecurityBlocked()
    {
        var recorder = new RecordingDebugCoordinator();
        var schema = Schema(
            Objects(
                Start(),
                Action("rest", "restCall", new
                {
                    method = "GET",
                    url = "https://example.com/api/test",
                    request = new { method = "GET", urlExpression = new { raw = "'https://example.com/api/test'" } }
                }),
                End()),
            Flows(
                Flow("f1", "start", "rest"),
                Flow("f2", "rest", "end")));

        var session = await RunWithRegistryAsync(schema, debugSessionId: "debug-session-1", debugCoordinator: recorder);

        Assert.Equal("failed", session.Status);
        Assert.Contains(recorder.SafePoints, item => item.Phase == MicroflowDebugPausePhase.BeforeRestRequest && item.SemanticKind == "rest");
        Assert.Contains(recorder.SafePoints, item => item.Phase == MicroflowDebugPausePhase.AfterRestHandled && item.SemanticKind == "rest");
    }

    [Fact]
    public async Task Run_AggregateListAction_ProducesNumericVariableThroughRegistry()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("count", "aggregateList", new
                {
                    sourceListVariableName = "items",
                    aggregateFunction = "count",
                    outputVariableName = "total",
                    emptyListBehavior = "zero"
                }),
                End(returnValue: "$total")),
            Flows(
                Flow("f1", "start", "count"),
                Flow("f2", "count", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("success", session.Status);
        // The registry-backed AggregateListActionExecutor advances the flow and
        // contributes a "total" variable that End can return.
        Assert.NotNull(session.Output);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "count" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_CreateObjectAction_ExecutesAndContinuesViaRegistry()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("create", "createObject", new
                {
                    entityType = "Demo.Order",
                    objectVariableName = "order",
                    value = new { customerId = "C-001", total = 200 }
                }),
                End()),
            Flows(
                Flow("f1", "start", "create"),
                Flow("f2", "create", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "create" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_NanoflowOnlyAction_ProducesRuntimeCommandAndContinues()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("nf", "callNanoflow", new { targetMicroflowId = "demo" }),
                End()),
            Flows(
                Flow("f1", "start", "nf"),
                Flow("f2", "nf", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Null(session.Error);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "nf" && frame.Status == "success");
        var nanoflowFrame = Assert.Single(session.Trace, frame => frame.ObjectId == "nf");
        Assert.Contains("runtimeCommands", nanoflowFrame.Output?.GetRawText() ?? string.Empty);
        Assert.Contains("callNanoflow", nanoflowFrame.Output?.GetRawText() ?? string.Empty);
    }

    [Fact]
    public async Task Run_ThrowExceptionAction_FailsRunWithStructuredError()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("throw", "throwException", new
                {
                    errorCode = "DEMO_ERROR",
                    message = "Something happened"
                }),
                End()),
            Flows(
                Flow("f1", "start", "throw"),
                Flow("f2", "throw", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal("DEMO_ERROR", session.Error?.Code);
        Assert.Equal("Something happened", session.Error?.Message);
    }

    [Fact]
    public async Task Run_FilterList_FiltersItemsByExpression()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("create", "createList", new
                {
                    outputVariableName = "items",
                    items = new[] { new { score = 70 }, new { score = 90 }, new { score = 110 } }
                }),
                Action("filter", "filterList", new
                {
                    listVariableName = "items",
                    outputVariableName = "passed",
                    itemVariableName = "$item",
                    conditionExpression = "$item/score > 80"
                }),
                End(returnValue: "$passed")),
            Flows(
                Flow("f1", "start", "create"),
                Flow("f2", "create", "filter"),
                Flow("f3", "filter", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.NotNull(session.Output);
        Assert.Equal(JsonValueKind.Array, session.Output!.Value.ValueKind);
        Assert.Equal(2, session.Output.Value.GetArrayLength());
    }

    [Fact]
    public async Task Run_SortList_OrdersByFieldAscending()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("create", "createList", new
                {
                    outputVariableName = "items",
                    items = new[] { new { score = 70 }, new { score = 110 }, new { score = 90 } }
                }),
                Action("sort", "sortList", new
                {
                    listVariableName = "items",
                    outputVariableName = "ordered",
                    sortField = "score",
                    direction = "asc"
                }),
                End(returnValue: "$ordered")),
            Flows(
                Flow("f1", "start", "create"),
                Flow("f2", "create", "sort"),
                Flow("f3", "sort", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.NotNull(session.Output);
        var output = session.Output!.Value;
        Assert.Equal(JsonValueKind.Array, output.ValueKind);
        var ordered = output.EnumerateArray().Select(item => item.GetProperty("score").GetInt32()).ToArray();
        Assert.Equal(new[] { 70, 90, 110 }, ordered);
    }

    [Fact]
    public async Task Run_RetrieveAction_ExecutesAndReturnsItemsViaRegistry()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("retrieve", "retrieve", new
                {
                    entityType = "Demo.Order",
                    resultVariableName = "orders",
                    limit = 10
                }),
                End()),
            Flows(
                Flow("f1", "start", "retrieve"),
                Flow("f2", "retrieve", "end")));

        var session = await RunWithRegistryAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "retrieve" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ActionContext_CarriesConnectorCapabilitiesFromOptions()
    {
        var probe = new ConnectorCapabilityProbeExecutor();
        var schema = Schema(
            Objects(
                Start(),
                Action("probe", probe.ActionKind, new { }),
                End()),
            Flows(
                Flow("f1", "start", "probe"),
                Flow("f2", "probe", "end")));

        var session = await RunWithRegistryAsync(
            schema,
            options: new MicroflowTestRunOptionsDto { ConnectorCapabilities = new[] { "rest", "soap" } },
            configureServices: services =>
            {
                services.AddScoped<IMicroflowActionExecutorRegistry>(_ =>
                {
                    var registry = new MicroflowActionExecutorRegistry();
                    registry.Register(probe);
                    return registry;
                });
            });

        Assert.Equal("success", session.Status);
        Assert.Equal(new[] { "rest", "soap" }, probe.SeenConnectorCapabilities);
    }

    private static async Task<MicroflowRunSessionDto> RunWithRegistryAsync(
        JsonElement schema,
        IReadOnlyDictionary<string, object?>? input = null,
        string? debugSessionId = null,
        IMicroflowDebugCoordinator? debugCoordinator = null,
        MicroflowTestRunOptionsDto? options = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (debugCoordinator is not null)
        {
            services.AddSingleton(debugCoordinator);
        }
        services.AddAtlasApplicationMicroflows();
        services.AddSingleton<IMicroflowClock>(new FixedClock());
        services.AddSingleton<IMicroflowRequestContextAccessor>(new InMemoryRequestContextAccessor());
        // Provide stub repositories - the engine gracefully degrades when these are empty;
        // CallMicroflow tests provide their own mocks.
        services.AddSingleton(Substitute.For<IMicroflowResourceRepository>());
        services.AddSingleton(Substitute.For<IMicroflowSchemaSnapshotRepository>());
        services.AddSingleton(Substitute.For<IMicroflowVersionRepository>());
        services.AddSingleton(Substitute.For<IMicroflowPublishSnapshotRepository>());
        services.AddSingleton(Substitute.For<IMicroflowFolderRepository>());
        services.AddSingleton(Substitute.For<IMicroflowReferenceRepository>());
        services.AddSingleton(Substitute.For<IMicroflowRunRepository>());
        services.AddSingleton(Substitute.For<IMicroflowMetadataCacheRepository>());
        services.AddSingleton(Substitute.For<IMicroflowStorageTransaction>());
        services.AddSingleton(Substitute.For<IMicroflowStorageDiagnosticsService>());
        configureServices?.Invoke(services);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IMicroflowRuntimeEngine>();
        var jsonInput = (input ?? new Dictionary<string, object?>())
            .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions), StringComparer.Ordinal);

        return await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-test",
                SchemaId = "schema-test",
                Version = "1",
                Schema = schema,
                Input = jsonInput,
                Options = options ?? new MicroflowTestRunOptionsDto(),
                RequestContext = new MicroflowRequestContext { TraceId = Guid.NewGuid().ToString("N") },
                DebugSessionId = debugSessionId,
                MaxCallDepth = 10
            },
            CancellationToken.None);
    }

    private static JsonElement Schema(IReadOnlyList<object> objects, IReadOnlyList<object> flows, IReadOnlyList<object>? parameters = null)
        => MicroflowDesignSchemaTestFactory.Schema(objects, flows, parameters, "mf-test", JsonOptions);

    private static IReadOnlyList<object> Objects(params object[] objects) => objects;

    private static IReadOnlyList<object> Flows(params object[] flows) => flows;

    private static IReadOnlyList<object> Parameters(params object[] parameters) => parameters;

    private static object Parameter(string name, string dataType, bool required)
        => new { id = name, name, dataType, required };

    private static object Start(string id = "start")
        => new { id, kind = "startEvent", caption = "Start" };

    private static object End(string id = "end", string? returnValue = null)
        => returnValue is null
            ? new { id, kind = "endEvent", caption = "End" }
            : new { id, kind = "endEvent", caption = "End", returnValue };

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

    private sealed class InMemoryRequestContextAccessor : IMicroflowRequestContextAccessor
    {
        public MicroflowRequestContext Current { get; set; } = new()
        {
            TraceId = Guid.NewGuid().ToString("N"),
            UserId = "system",
            UserName = "system",
            WorkspaceId = "ws-test",
            TenantId = "tenant-test"
        };
    }

    private sealed class ConnectorCapabilityProbeExecutor : IMicroflowActionExecutor
    {
        public string ActionKind => "connectorCapabilityProbe";

        public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

        public string SupportLevel => MicroflowActionSupportLevel.Supported;

        public IReadOnlyList<string> SeenConnectorCapabilities { get; private set; } = Array.Empty<string>();

        public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
        {
            SeenConnectorCapabilities = context.Options.ConnectorCapabilities;
            return Task.FromResult(new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Success
            });
        }
    }

    private sealed class RecordingDebugCoordinator : IMicroflowDebugCoordinator
    {
        public List<MicroflowDebugSafePoint> SafePoints { get; } = [];

        public Task WaitAtSafePointAsync(
            string? debugSessionId,
            string engineRunId,
            MicroflowDebugSafePoint point,
            CancellationToken cancellationToken)
        {
            SafePoints.Add(point);
            return Task.CompletedTask;
        }

        public Task WaitAtSafePointAsync(
            string? debugSessionId,
            string engineRunId,
            MicroflowDebugSafePoint point,
            MicroflowDebugRuntimeSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            SafePoints.Add(point);
            return Task.CompletedTask;
        }

        public void ReleaseOnePause(string debugSessionId)
        {
        }

        public MicroflowDebugSession? ApplyCommand(string debugSessionId, DebugCommand command)
            => null;

        public void RemoveSession(string debugSessionId)
        {
        }
    }
}
