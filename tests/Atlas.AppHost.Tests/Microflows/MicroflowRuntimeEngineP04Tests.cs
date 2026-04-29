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
    public async Task Run_LoopedActivity_FailsExplicitlyAsUnsupported()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "loop", kind = "loopedActivity", caption = "Loop" }),
            Flows(Flow("f1", "start", "loop")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeUnsupportedAction, session.Error?.Code);
    }

    [Fact]
    public async Task Run_ParallelGateway_FailsAsUnsupportedNotSilentSuccess()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "fork", kind = "parallelGateway", caption = "Fork" }),
            Flows(Flow("f1", "start", "fork")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeUnsupportedAction, session.Error?.Code);
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
    public async Task Run_PendingClientCommand_FailsExplicitly()
    {
        // showPage 注册为 RuntimeCommand，executor 返回 PendingClientCommand。
        // 之前引擎会当作 success 继续 ContinueAfterAction，导致 server-only test-run 形成假成功。
        var schema = Schema(
            Objects(
                Start(),
                Action("show", "showPage", new { pageId = "p1" }),
                End()),
            Flows(
                Flow("f1", "start", "show"),
                Flow("f2", "show", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimePendingClientCommand, session.Error?.Code);
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
        => JsonSerializer.SerializeToElement(new
        {
            schemaVersion = "1.0.0",
            id = "mf-p04",
            name = "mf-p04",
            displayName = "mf-p04",
            moduleId = "mod",
            parameters = Array.Empty<object>(),
            returnType = new { kind = "unknown" },
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
