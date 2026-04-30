using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-4 覆盖：
///   1. 之前 _ => Unsupported 兜底导致 errorEvent / loopedActivity / parallelGateway / inclusiveGateway / annotation 静默失败。
///   2. 之前 PendingClientCommand 被当作 success 继续推进流。
///   3. 之前 ShouldEnterErrorHandler 没有跳错误分支。
/// </summary>
public sealed class MicroflowRuntimeEngineP04Tests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Run_ErrorEvent_ProducesRuntimeErrorEventReached()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "thrower", kind = "errorEvent", caption = "Throw", errorCode = "DOMAIN_FAILED", message = "boom" }),
            Flows(Flow("f1", "start", "thrower")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal("DOMAIN_FAILED", session.Error?.Code);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "thrower" && frame.Status == "failed");
    }

    [Fact]
    public async Task Run_LoopedActivityWithoutBody_FailsWithLoopBodyNotFound()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "loop", kind = "loopedActivity", caption = "Loop" }),
            Flows(Flow("f1", "start", "loop")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeLoopBodyNotFound, session.Error?.Code);
    }

    [Fact]
    public async Task Run_LoopedActivity_With_CustomErrorHandler_Routes_To_Handler()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "loop", kind = "loopedActivity", caption = "Loop", errorHandlingType = "customWithoutRollback" },
                new { id = "handler", kind = "exclusiveMerge", caption = "Handler" },
                End(returnValue: "$latestError")),
            Flows(
                Flow("f1", "start", "loop"),
                ErrorFlow("f2-err", "loop", "handler"),
                Flow("f3", "handler", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "handler");
    }

    [Fact]
    public async Task Run_LoopedActivity_With_Continue_ErrorHandling_Continues_Normal_Flow()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "loop", kind = "loopedActivity", caption = "Loop", errorHandlingType = "continue" },
                End()),
            Flows(
                Flow("f1", "start", "loop"),
                Flow("f2", "loop", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "end" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ParallelGateway_FailsWhenOutgoingFlowMissing()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "fork", kind = "parallelGateway", caption = "Fork" }),
            Flows(Flow("f1", "start", "fork")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeFlowNotFound, session.Error?.Code);
    }

    [Fact]
    public async Task Run_ParallelGateway_UsesRuntimeMainPath()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "fork", kind = "parallelGateway", caption = "Fork" },
                End()),
            Flows(
                Flow("f1", "start", "fork"),
                Flow("f2", "fork", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "fork" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_AnnotationNode_DoesNotBlockSuccessfulFlow()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "note", kind = "annotation", caption = "Comment", text = "design note" },
                End()),
            Flows(
                Flow("f1", "start", "note"),
                Flow("f2", "note", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "note" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_PendingClientCommand_Continues_And_Emits_RuntimeCommand()
    {
        // showPage 注册为 RuntimeCommand，executor 返回 PendingClientCommand。
        // 当前 runtime 会把命令写入 trace/output，然后继续 normal flow，
        // 由前端在同一 run session 中消费这些命令。
        var schema = Schema(
            Objects(
                Start(),
                Action("show", "showPage", new { pageId = "p1" }),
                End()),
            Flows(
                Flow("f1", "start", "show"),
                Flow("f2", "show", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        var showFrame = session.Trace.Single(frame => frame.ObjectId == "show");
        Assert.Equal("success", showFrame.Status);
        Assert.Contains(showFrame.Output?.GetRawText() ?? string.Empty, "runtimeCommands");
    }

    [Theory]
    [InlineData("counter", "{ \"metricName\": \"orders.total\", \"valueExpression\": { \"raw\": \"1\" } }")]
    [InlineData("incrementCounter", "{ \"metricName\": \"orders.increment\" }")]
    [InlineData("gauge", "{ \"metricName\": \"orders.gauge\", \"valueExpression\": { \"raw\": \"2\" } }")]
    public async Task Run_MetricsActions_Succeed(string actionKind, string rawAction)
    {
        var actionJson = JsonDocument.Parse(rawAction).RootElement.Clone();
        var schema = Schema(
            Objects(
                Start(),
                Action("metric", actionKind, actionJson),
                End()),
            Flows(
                Flow("f1", "start", "metric"),
                Flow("f2", "metric", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "metric" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ThrowExceptionWithErrorHandlerEdge_RoutesToErrorHandlerBranch()
    {
        // throwException action 设置 ShouldEnterErrorHandler=true。
        // 现在引擎在该标志 + isErrorHandler 边存在时跳 error handler 路径而不是 fail run。
        var schema = Schema(
            Objects(
                Start(),
                Action("thrower", "throwException", new
                {
                    code = "BIZ_FAIL",
                    message = "primary path failed"
                }),
                new { id = "handler", kind = "exclusiveMerge", caption = "Handler" },
                End(returnValue: "$latestError")),
            Flows(
                Flow("f1", "start", "thrower"),
                ErrorFlow("f2-err", "thrower", "handler"),
                Flow("f3", "handler", "end")));

        var session = await RunAsync(schema);

        // 错误分支兜住后流程仍然成功结束（但首节点 trace 标 failed）。
        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "thrower" && frame.Status == "failed");
        Assert.Contains(session.Trace, frame => frame.ObjectId == "handler");
    }

    private static async Task<MicroflowRunSessionDto> RunAsync(JsonElement schema, IReadOnlyDictionary<string, object?>? input = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtlasApplicationMicroflows();
        services.AddSingleton<IMicroflowClock>(new FixedClock());
        services.AddSingleton<IMicroflowRequestContextAccessor>(new InMemoryRequestContextAccessor());
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

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IMicroflowRuntimeEngine>();
        var jsonInput = (input ?? new Dictionary<string, object?>())
            .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions), StringComparer.Ordinal);

        return await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-p04",
                SchemaId = "schema-p04",
                Version = "1",
                Schema = schema,
                Input = jsonInput,
                Options = new MicroflowTestRunOptionsDto(),
                RequestContext = new MicroflowRequestContext { TraceId = Guid.NewGuid().ToString("N") },
                MaxCallDepth = 10
            },
            CancellationToken.None);
    }

    private static JsonElement Schema(IReadOnlyList<object> objects, IReadOnlyList<object> flows)
        => MicroflowDesignSchemaTestFactory.Schema(objects, flows, Array.Empty<object>(), "mf-p04", JsonOptions);

    private static IReadOnlyList<object> Objects(params object[] objects) => objects;

    private static IReadOnlyList<object> Flows(params object[] flows) => flows;

    private static object Start(string id = "start") => new { id, kind = "startEvent", caption = "Start" };

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

    private static object ErrorFlow(string id, string source, string target)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = Array.Empty<object>(),
            isErrorHandler = true,
            editor = new { edgeKind = "errorHandler" }
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
            WorkspaceId = "ws-p04",
            TenantId = "tenant-p04"
        };
    }
}
