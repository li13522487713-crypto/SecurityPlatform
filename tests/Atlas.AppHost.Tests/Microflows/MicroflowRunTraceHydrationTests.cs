using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Audit;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Debug;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Application.Microflows.Services;
using Atlas.Domain.Microflows.Entities;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowRunTraceHydrationTests
{
    [Fact]
    public async Task GetRunTrace_RehydratesNodeIoFieldsFromExtraJson()
    {
        var runRepository = Substitute.For<IMicroflowRunRepository>();
        var ownershipGuard = Substitute.For<IMicroflowRunOwnershipGuard>();
        ownershipGuard.EnsureRunOwnedAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(new MicroflowRunSessionEntity { Id = "run-1", ResourceId = "mf-1", WorkspaceId = "ws-1", TenantId = "tenant-1" });
        runRepository.ListTraceFramesAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new MicroflowRunTraceFrameEntity
                {
                    Id = "frame-1",
                    RunId = "run-1",
                    ObjectId = "change-score",
                    ActionId = "change-score-action",
                    CollectionId = "root",
                    IncomingFlowId = "flow-in",
                    OutgoingFlowId = "flow-out",
                    SelectedCaseValueJson = "{\"kind\":\"boolean\",\"value\":true,\"persistedValue\":\"true\"}",
                    Status = "success",
                    StartedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
                    EndedAt = DateTimeOffset.Parse("2026-05-02T00:00:01Z"),
                    DurationMs = 1000,
                    InputJson = "{\"score\":1}",
                    OutputJson = "{\"score\":2}",
                    ExtraJson = """
                    {
                      "microflowId": "mf-1",
                      "nodeKind": "actionActivity",
                      "actionKind": "changeVariable",
                      "inputVariables": { "score": { "name": "score", "valuePreview": "1", "rawValue": 1, "type": { "kind": "integer" } } },
                      "actionInput": { "targetVariableName": "score" },
                      "evaluatedExpressions": [{ "expression": "$score + 1", "value": 2 }],
                      "outputVariables": { "score": { "name": "score", "valuePreview": "2", "rawValue": 2, "type": { "kind": "integer" } } },
                      "variableDelta": { "added": [], "changed": ["score"], "removed": [] },
                      "handoffPayload": { "outgoingFlowId": "flow-out", "output": { "score": 2 } },
                      "transactionEffect": { "status": "none" }
                    }
                    """
                }
            });
        runRepository.ListLogsAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MicroflowRunLogEntity>());
        var service = CreateService(runRepository, ownershipGuard);

        var response = await service.GetRunTraceAsync("run-1", CancellationToken.None);

        var frame = Assert.Single(response.Trace);
        Assert.Equal("mf-1", frame.MicroflowId);
        Assert.Equal("actionActivity", frame.NodeKind);
        Assert.Equal("changeVariable", frame.ActionKind);
        Assert.True(frame.InputVariables.HasValue);
        Assert.True(frame.InputVariables.Value.TryGetProperty("score", out var inputScore));
        Assert.Equal(1, inputScore.GetProperty("rawValue").GetInt32());
        Assert.True(frame.ActionInput.HasValue);
        Assert.Equal("score", frame.ActionInput.Value.GetProperty("targetVariableName").GetString());
        Assert.True(frame.EvaluatedExpressions.HasValue);
        Assert.Equal("$score + 1", frame.EvaluatedExpressions.Value[0].GetProperty("expression").GetString());
        Assert.True(frame.OutputVariables.HasValue);
        Assert.Equal(2, frame.OutputVariables.Value.GetProperty("score").GetProperty("rawValue").GetInt32());
        Assert.True(frame.VariableDelta.HasValue);
        Assert.Equal("score", frame.VariableDelta.Value.GetProperty("changed")[0].GetString());
        Assert.True(frame.HandoffPayload.HasValue);
        Assert.Equal("flow-out", frame.HandoffPayload.Value.GetProperty("outgoingFlowId").GetString());
        Assert.True(frame.TransactionEffect.HasValue);
        Assert.Equal("none", frame.TransactionEffect.Value.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetRunSession_RehydratesCallStackFramesFromSessionExtraJson()
    {
        var runRepository = Substitute.For<IMicroflowRunRepository>();
        var ownershipGuard = Substitute.For<IMicroflowRunOwnershipGuard>();
        ownershipGuard.EnsureRunOwnedAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(new MicroflowRunSessionEntity { Id = "run-1", ResourceId = "mf-parent", WorkspaceId = "ws-1", TenantId = "tenant-1" });
        runRepository.GetSessionAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(new MicroflowRunSessionEntity
            {
                Id = "run-1",
                ResourceId = "mf-parent",
                SchemaSnapshotId = "schema-parent",
                Status = "success",
                InputJson = "{}",
                StartedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
                EndedAt = DateTimeOffset.Parse("2026-05-02T00:00:01Z"),
                ExtraJson = """
                {
                  "version": "1.0.0",
                  "parentRunId": "run-root",
                  "rootRunId": "run-root",
                  "callDepth": 1,
                  "correlationId": "corr-1",
                  "callStack": ["MF_Parent", "MF_Child"],
                  "callStackFrames": [
                    {
                      "id": "frame-call-1",
                      "runId": "run-1",
                      "parentRunId": "run-root",
                      "rootRunId": "run-root",
                      "microflowId": "mf-child",
                      "schemaId": "schema-child",
                      "version": "1.0.0",
                      "qualifiedName": "Sales.MF_Child",
                      "callerObjectId": "call-order-submit",
                      "callerActionId": "action-call-order-submit",
                      "depth": 1,
                      "callMode": "sync",
                      "status": "success",
                      "startedAt": "2026-05-02T00:00:00Z",
                      "endedAt": "2026-05-02T00:00:01Z",
                      "durationMs": 1000
                    }
                  ],
                  "childRunIds": []
                }
                """
            });
        runRepository.ListTraceFramesAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MicroflowRunTraceFrameEntity>());
        runRepository.ListLogsAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MicroflowRunLogEntity>());
        var service = CreateService(runRepository, ownershipGuard);

        var session = await service.GetRunSessionAsync("run-1", CancellationToken.None);

        var frame = Assert.Single(session.CallStackFrames);
        Assert.Equal("frame-call-1", frame.Id);
        Assert.Equal("mf-child", frame.MicroflowId);
        Assert.Equal("Sales.MF_Child", frame.QualifiedName);
        Assert.Equal("call-order-submit", frame.CallerObjectId);
        Assert.Equal("action-call-order-submit", frame.CallerActionId);
        Assert.Equal("success", frame.Status);
    }

    private static MicroflowTestRunService CreateService(
        IMicroflowRunRepository runRepository,
        IMicroflowRunOwnershipGuard ownershipGuard)
    {
        var requestContextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        requestContextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "ws-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-1"
        });

        return new MicroflowTestRunService(
            Substitute.For<IMicroflowResourceRepository>(),
            Substitute.For<IMicroflowSchemaSnapshotRepository>(),
            runRepository,
            Substitute.For<IMicroflowStorageTransaction>(),
            Substitute.For<IMicroflowValidationService>(),
            Substitute.For<IMicroflowMetadataService>(),
            Substitute.For<IMicroflowRuntimeEngine>(),
            Substitute.For<IMicroflowExecutionPlanLoader>(),
            requestContextAccessor,
            Substitute.For<IMicroflowAuditWriter>(),
            Substitute.For<IMicroflowClock>(),
            Substitute.For<IMicroflowRunCancellationRegistry>(),
            ownershipGuard,
            Substitute.For<IDebugSessionStore>());
    }
}
