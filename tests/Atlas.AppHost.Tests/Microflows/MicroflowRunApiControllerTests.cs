using Atlas.AppHost.Microflows.Controllers;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
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
