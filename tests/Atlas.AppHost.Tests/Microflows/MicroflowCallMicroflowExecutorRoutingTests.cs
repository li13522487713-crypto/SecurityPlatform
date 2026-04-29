using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Domain.Microflows.Entities;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-5 验证：当 DI 中注册了 CallMicroflowActionExecutor 时，Engine 不再走内联 ExecuteCallMicroflowAsync，
/// 而是统一通过 Action Executor Registry 调用，确保单一执行入口；当 executor 未注册时退回内联路径。
/// </summary>
public sealed class MicroflowCallMicroflowExecutorRoutingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Run_CallMicroflow_PrefersRegisteredExecutorOverInlinePath()
    {
        const string childId = "mf-child-routed";
        const string childSnapshot = "snapshot-child-routed";

        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        resourceRepository.GetByIdAsync(childId, Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity
            {
                Id = childId,
                Version = "1",
                CurrentSchemaSnapshotId = childSnapshot
            });

        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        snapshotRepository.GetByIdAsync(childSnapshot, Arg.Any<CancellationToken>())
            .Returns(new MicroflowSchemaSnapshotEntity
            {
                Id = childSnapshot,
                ResourceId = childId,
                SchemaJson = ChildSchema(childId).GetRawText()
            });

        var trackingExecutor = new TrackingCallMicroflowExecutor();

        var services = BuildServices(resourceRepository, snapshotRepository);
        services.AddScoped<CallMicroflowActionExecutor>(_ => throw new InvalidOperationException("specialized executor not used"));
        // 注入 tracking executor 到 registry：通过覆盖 IMicroflowActionExecutorRegistry。
        services.AddScoped<IMicroflowActionExecutorRegistry>(sp => new TrackingRegistry(trackingExecutor));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IMicroflowRuntimeEngine>();

        var parent = ParentSchema(childId);
        var session = await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-parent-routed",
                SchemaId = "schema-parent-routed",
                Version = "1",
                Schema = parent,
                Input = new Dictionary<string, JsonElement>(),
                Options = new MicroflowTestRunOptionsDto(),
                RequestContext = new MicroflowRequestContext { TraceId = Guid.NewGuid().ToString("N") },
                MaxCallDepth = 5
            },
            CancellationToken.None);

        // tracking executor 必须被调到，否则证明引擎仍走了内联 ExecuteCallMicroflowAsync。
        Assert.Equal(1, trackingExecutor.CallCount);
        Assert.Equal("success", session.Status);
        Assert.Equal("routed", session.Output?.GetString());
    }

    private static ServiceCollection BuildServices(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository snapshotRepository)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtlasApplicationMicroflows();
        services.AddSingleton<IMicroflowClock>(new FixedClock());
        services.AddSingleton<IMicroflowRequestContextAccessor>(new InMemoryRequestContextAccessor());
        services.AddSingleton(resourceRepository);
        services.AddSingleton(snapshotRepository);
        services.AddSingleton(Substitute.For<IMicroflowVersionRepository>());
        services.AddSingleton(Substitute.For<IMicroflowPublishSnapshotRepository>());
        services.AddSingleton(Substitute.For<IMicroflowFolderRepository>());
        services.AddSingleton(Substitute.For<IMicroflowReferenceRepository>());
        services.AddSingleton(Substitute.For<IMicroflowRunRepository>());
        services.AddSingleton(Substitute.For<IMicroflowMetadataCacheRepository>());
        services.AddSingleton(Substitute.For<IMicroflowStorageTransaction>());
        services.AddSingleton(Substitute.For<IMicroflowStorageDiagnosticsService>());
        return services;
    }

    private static JsonElement ParentSchema(string childResourceId)
        => Schema(
            Objects(
                Start(),
                new
                {
                    id = "call",
                    kind = "actionActivity",
                    caption = "call",
                    action = new
                    {
                        id = "call-action",
                        kind = "callMicroflow",
                        officialType = "Microflows$CallMicroflowAction",
                        targetMicroflowId = childResourceId,
                        callMode = "sync",
                        outputVariableName = "result"
                    }
                },
                End(returnValue: "$result")),
            Flows(Flow("f1", "start", "call"), Flow("f2", "call", "end")));

    private static JsonElement ChildSchema(string id)
        => Schema(
            Objects(Start(), End(returnValue: "\"routed\"")),
            Flows(Flow("f1", "start", "end")),
            id: id);

    private static JsonElement Schema(IReadOnlyList<object> objects, IReadOnlyList<object> flows, string id = "mf")
        => JsonSerializer.SerializeToElement(new
        {
            schemaVersion = "1.0.0",
            id,
            name = id,
            displayName = id,
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

    private sealed class TrackingRegistry : IMicroflowActionExecutorRegistry
    {
        private readonly TrackingCallMicroflowExecutor _tracking;

        public TrackingRegistry(TrackingCallMicroflowExecutor tracking) => _tracking = tracking;

        public void Register(IMicroflowActionExecutor executor)
        {
            // no-op: this stub registry only routes callMicroflow.
        }

        public bool TryGet(string? actionKind, out IMicroflowActionExecutor executor)
        {
            if (string.Equals(actionKind, "callMicroflow", StringComparison.OrdinalIgnoreCase))
            {
                executor = _tracking;
                return true;
            }

            executor = null!;
            return false;
        }

        public IMicroflowActionExecutor GetOrFallback(string? actionKind) => _tracking;

        public string GetSupportLevel(string? actionKind) => "supported";

        public string GetCategory(string? actionKind) => "callMicroflow";

        public IReadOnlyList<MicroflowActionExecutorDescriptor> ListAll()
            => Array.Empty<MicroflowActionExecutorDescriptor>();

        public MicroflowActionExecutorCoverageDiagnostic ValidateCoverage(IEnumerable<string> actionKinds)
            => new();

        public void EnsureEveryActionKindCovered(IEnumerable<string> actionKinds)
        {
            // no-op
        }
    }

    private sealed class TrackingCallMicroflowExecutor : IMicroflowActionExecutor
    {
        public int CallCount { get; private set; }

        public string ActionKind => "callMicroflow";

        public string Category => "callMicroflow";

        public string SupportLevel => "supported";

        public Task<MicroflowActionExecutionResult> ExecuteAsync(
            MicroflowActionExecutionContext context,
            CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Success,
                ProducedVariables = new[]
                {
                    new MicroflowRuntimeVariableValueDto
                    {
                        Name = "result",
                        Type = JsonSerializer.SerializeToElement(new { kind = "String" }),
                        RawValue = JsonSerializer.SerializeToElement("routed"),
                    }
                },
                OutputJson = JsonSerializer.SerializeToElement(new { result = "routed" })
            });
        }
    }

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
            WorkspaceId = "ws",
            TenantId = "tenant"
        };
    }
}
