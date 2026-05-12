using Atlas.AppHost.Microflows.Controllers;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowRunApiControllerTests
{
    [Fact]
    public async Task EnqueueRun_returns_enqueued_run()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.EnqueueAsync(Arg.Any<EnqueueMicroflowRunRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EnqueueMicroflowRunResponse
            {
                RunId = "run-queue-1",
                ResourceId = "mf-1",
                Status = "queued",
                StartedAt = DateTimeOffset.Parse("2026-05-06T12:00:00Z")
            }));
        var controller = CreateController(testRunService);

        var result = await controller.EnqueueRun(
            new EnqueueMicroflowRunRequestDto
            {
                ResourceId = "mf-1",
                Request = new TestRunMicroflowApiRequest()
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<EnqueueMicroflowRunResponse>>(ok.Value);
        Assert.Equal("run-queue-1", envelope.Data?.RunId);
        Assert.Equal("queued", envelope.Data?.Status);
        await testRunService.Received(1).EnqueueAsync(
            Arg.Is<EnqueueMicroflowRunRequestDto>(item => item.ResourceId == "mf-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunStatus_returns_status_snapshot()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.GetRunStatusAsync("run-100", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MicroflowRunStatusDto
            {
                RunId = "run-100",
                ResourceId = "mf-2",
                Status = "running"
            }));
        var controller = CreateController(testRunService);

        var result = await controller.GetRunStatus("run-100", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowRunStatusDto>>(ok.Value);
        Assert.Equal("running", envelope.Data?.Status);
        await testRunService.Received(1).GetRunStatusAsync("run-100", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRun_returns_call_stack_frames()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.GetRunSessionAsync("run-200", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MicroflowRunSessionDto
            {
                Id = "run-200",
                ResourceId = "mf-parent",
                SchemaId = "schema-parent",
                Status = "success",
                StartedAt = DateTimeOffset.Parse("2026-05-12T03:00:00Z"),
                Input = new Dictionary<string, JsonElement>(),
                Trace = Array.Empty<MicroflowTraceFrameDto>(),
                Logs = Array.Empty<MicroflowRuntimeLogDto>(),
                Variables = Array.Empty<MicroflowRunSessionVariableSnapshotDto>(),
                CallStackFrames =
                [
                    new MicroflowRunCallStackFrameDto
                    {
                        Id = "frame-1",
                        RunId = "run-200",
                        RootRunId = "run-200",
                        MicroflowId = "mf-child",
                        QualifiedName = "Sales.Child",
                        CallerObjectId = "call-child",
                        Depth = 1,
                        CallMode = "sync",
                        Status = "success"
                    }
                ]
            }));
        var controller = CreateController(testRunService);

        var result = await controller.GetRun("run-200", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowRunSessionDto>>(ok.Value);
        var frame = Assert.Single(envelope.Data?.CallStackFrames ?? Array.Empty<MicroflowRunCallStackFrameDto>());
        Assert.Equal("call-child", frame.CallerObjectId);
        Assert.Equal("Sales.Child", frame.QualifiedName);
        await testRunService.Received(1).GetRunSessionAsync("run-200", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunByMicroflow_returns_call_stack_frames()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.GetRunSessionAsync("mf-parent", "run-201", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MicroflowRunSessionDto
            {
                Id = "run-201",
                ResourceId = "mf-parent",
                SchemaId = "schema-parent",
                Status = "success",
                StartedAt = DateTimeOffset.Parse("2026-05-12T03:05:00Z"),
                Input = new Dictionary<string, JsonElement>(),
                Trace = Array.Empty<MicroflowTraceFrameDto>(),
                Logs = Array.Empty<MicroflowRuntimeLogDto>(),
                Variables = Array.Empty<MicroflowRunSessionVariableSnapshotDto>(),
                CallStackFrames =
                [
                    new MicroflowRunCallStackFrameDto
                    {
                        Id = "frame-2",
                        RunId = "run-201",
                        RootRunId = "run-201",
                        MicroflowId = "mf-child",
                        QualifiedName = "Sales.Child",
                        CallerObjectId = "call-child",
                        CallerActionId = "action-call-child",
                        Depth = 1,
                        CallMode = "sync",
                        Status = "success"
                    }
                ]
            }));
        var controller = CreateController(testRunService);

        var result = await controller.GetRunByMicroflow("mf-parent", "run-201", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowRunSessionDto>>(ok.Value);
        var frame = Assert.Single(envelope.Data?.CallStackFrames ?? Array.Empty<MicroflowRunCallStackFrameDto>());
        Assert.Equal("action-call-child", frame.CallerActionId);
        Assert.Equal("mf-child", frame.MicroflowId);
        await testRunService.Received(1).GetRunSessionAsync("mf-parent", "run-201", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListRuns_returns_history_metadata()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.ListRunsAsync("mf-parent", Arg.Any<ListMicroflowRunsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ListMicroflowRunsResponse
            {
                Items =
                [
                    new MicroflowRunHistoryItemDto
                    {
                        RunId = "run-history-1",
                        MicroflowId = "mf-parent",
                        SchemaId = "schema-parent",
                        Status = "failed",
                        ErrorCode = "RuntimeCallMicroflowFailed",
                        ErrorMessage = "child failed",
                        DurationMs = 123,
                        StartedAt = DateTimeOffset.Parse("2026-05-12T04:00:00Z"),
                        CompletedAt = DateTimeOffset.Parse("2026-05-12T04:00:01Z"),
                        Finalized = true,
                        ParentRunId = "run-root",
                        RootRunId = "run-root",
                        CallFrameId = "frame-call-1",
                        CallDepth = 1,
                        CorrelationId = "corr-run-history",
                        TraceFrameCount = 5,
                        LogCount = 2,
                        ChildRunIds = ["run-child-1"],
                        CallStack = ["Sales.Parent", "Sales.Child"],
                        CallStackFrames =
                        [
                            new MicroflowRunCallStackFrameDto
                            {
                                Id = "frame-call-1",
                                RunId = "run-history-1",
                                MicroflowId = "mf-child",
                                QualifiedName = "Sales.Child",
                                CallerObjectId = "call-child",
                                Depth = 1,
                                Status = "failed"
                            }
                        ],
                        Summary = "Run failed"
                    }
                ],
                Total = 1
            }));
        var controller = CreateController(testRunService);

        var result = await controller.ListRuns("mf-parent", cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<ListMicroflowRunsResponse>>(ok.Value);
        var item = Assert.Single(envelope.Data?.Items ?? Array.Empty<MicroflowRunHistoryItemDto>());
        Assert.Equal("schema-parent", item.SchemaId);
        Assert.Equal("RuntimeCallMicroflowFailed", item.ErrorCode);
        Assert.Equal("run-root", item.ParentRunId);
        Assert.Equal("run-root", item.RootRunId);
        Assert.Equal("frame-call-1", item.CallFrameId);
        Assert.Equal(5, item.TraceFrameCount);
        Assert.Equal(new[] { "run-child-1" }, item.ChildRunIds);
        Assert.Equal(new[] { "Sales.Parent", "Sales.Child" }, item.CallStack);
        Assert.Equal("call-child", Assert.Single(item.CallStackFrames).CallerObjectId);
        await testRunService.Received(1).ListRunsAsync(
            "mf-parent",
            Arg.Is<ListMicroflowRunsRequest>(request => request.PageIndex == 1 && request.PageSize == 20 && request.Status == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryRun_returns_retry_result()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.RetryAsync("run-retry-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RetryMicroflowRunResponse
            {
                PreviousRunId = "run-retry-1",
                NewRunId = "run-retry-2",
                Status = "queued",
                StartedAt = DateTimeOffset.Parse("2026-05-06T12:30:00Z")
            }));
        var controller = CreateController(testRunService);

        var result = await controller.RetryRun("run-retry-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<RetryMicroflowRunResponse>>(ok.Value);
        Assert.Equal("run-retry-2", envelope.Data?.NewRunId);
        Assert.Equal("queued", envelope.Data?.Status);
        await testRunService.Received(1).RetryAsync("run-retry-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelRun_returns_cancelled_result()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.CancelAsync("run-cancel-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CancelMicroflowRunResponse
            {
                RunId = "run-cancel-1",
                Status = "cancelled"
            }));
        var controller = CreateController(testRunService);

        var result = await controller.Cancel("run-cancel-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<CancelMicroflowRunResponse>>(ok.Value);
        Assert.Equal("run-cancel-1", envelope.Data?.RunId);
        Assert.Equal("cancelled", envelope.Data?.Status);
        await testRunService.Received(1).CancelAsync("run-cancel-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunRetention_returns_retention_summary()
    {
        var testRunService = Substitute.For<IMicroflowTestRunService>();
        testRunService.RunRetentionAsync(Arg.Any<RunRetentionRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RunRetentionResultDto
            {
                CutoffAt = DateTimeOffset.Parse("2026-04-01T00:00:00Z"),
                DryRun = true,
                CandidateRunCount = 12,
                DeletedRunCount = 0,
                DeletedTraceCount = 0,
                DeletedLogCount = 0,
                SampleRunIds = ["run-1", "run-2"]
            }));
        var controller = CreateController(testRunService);

        var result = await controller.RunRetention(
            new RunRetentionRequestDto
            {
                RetentionDays = 30,
                DryRun = true,
                BatchSize = 100
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<RunRetentionResultDto>>(ok.Value);
        Assert.Equal(12, envelope.Data?.CandidateRunCount);
        Assert.True(envelope.Data?.DryRun);
        await testRunService.Received(1).RunRetentionAsync(
            Arg.Is<RunRetentionRequestDto>(item => item.RetentionDays == 30 && item.DryRun),
            Arg.Any<CancellationToken>());
    }

    private static MicroflowResourceController CreateController(IMicroflowTestRunService testRunService)
    {
        var contextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        contextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-run-api"
        });

        var controller = new MicroflowResourceController(
            Substitute.For<IMicroflowResourceService>(),
            Substitute.For<IMicroflowPublishService>(),
            Substitute.For<IMicroflowVersionService>(),
            Substitute.For<IMicroflowValidationService>(),
            Substitute.For<IMicroflowReferenceService>(),
            testRunService,
            Substitute.For<IMicroflowExecutionPlanLoader>(),
            Substitute.For<IMicroflowFlowNavigator>(),
            Substitute.For<IMicroflowStorageDiagnosticsService>(),
            Substitute.For<IMicroflowActionExecutorRegistry>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<IHostEnvironment>(),
            contextAccessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }
}
