using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class WorkflowV2ExecutionServiceTests
{
    [Fact]
    public async Task DebugNodeAsync_ShouldRunOnlyTargetNode_WithoutSyntheticEntryExit()
    {
        var tenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var workflowId = 4001L;
        var userId = 7001L;

        var metaRepo = Substitute.For<IWorkflowMetaRepository>();
        metaRepo.FindActiveByIdAsync(tenantId, workflowId, Arg.Any<CancellationToken>())
            .Returns(new WorkflowMeta(tenantId, "debug-flow", null, WorkflowMode.Standard, userId, workflowId));

        var draftRepo = Substitute.For<IWorkflowDraftRepository>();
        draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, Arg.Any<CancellationToken>())
            .Returns(new WorkflowDraft(
                tenantId,
                workflowId,
                """
                {
                  "nodes": [
                    {
                      "key": "text_1",
                      "type": 15,
                      "label": "文本处理",
                      "config": {
                        "template": "告警：{{incident.summary}}",
                        "outputKey": "rendered",
                        "inputMappings": {
                          "incident": "ticket.payload"
                        }
                      },
                      "layout": { "x": 120, "y": 80, "width": 220, "height": 80 }
                    }
                  ],
                  "connections": []
                }
                """,
                90001L));

        var versionRepo = Substitute.For<IWorkflowVersionRepository>();
        var executionRepo = Substitute.For<IWorkflowExecutionRepository>();
        executionRepo.AddAsync(Arg.Any<WorkflowExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        executionRepo.UpdateAsync(Arg.Any<WorkflowExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var capturedNodeExecutions = new List<WorkflowNodeExecution>();
        var nodeExecutionRepo = Substitute.For<IWorkflowNodeExecutionRepository>();
        nodeExecutionRepo.AddAsync(Arg.Any<WorkflowNodeExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        nodeExecutionRepo
            .When(x => x.AddAsync(Arg.Any<WorkflowNodeExecution>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => capturedNodeExecutions.Add(callInfo.Arg<WorkflowNodeExecution>()));
        nodeExecutionRepo.UpdateAsync(Arg.Any<WorkflowNodeExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        nodeExecutionRepo.ListByExecutionIdAsync(tenantId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var executionId = callInfo.ArgAt<long>(1);
                return capturedNodeExecutions
                    .Where(item => item.ExecutionId == executionId)
                    .ToList()
                    .AsReadOnly();
            });

        var idGenerator = Substitute.For<IIdGeneratorAccessor>();
        var nextId = 1000L;
        idGenerator.NextId().Returns(_ => Interlocked.Increment(ref nextId));
        var appContextAccessor = Substitute.For<IAppContextAccessor>();
        appContextAccessor.GetAppId().Returns(string.Empty);

        var registry = new NodeExecutorRegistry(new TextProcessorNodeExecutor());
        var provider = new ServiceCollection().BuildServiceProvider();
        var dagExecutor = new DagExecutor(
            registry,
            nodeExecutionRepo,
            executionRepo,
            idGenerator,
            provider,
            Substitute.For<ILogger<DagExecutor>>());

        var executionService = new WorkflowV2ExecutionService(
            metaRepo,
            draftRepo,
            versionRepo,
            executionRepo,
            nodeExecutionRepo,
            dagExecutor,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new WorkflowExecutionCancellationRegistry(),
            idGenerator,
            appContextAccessor,
            Substitute.For<ILogger<WorkflowV2ExecutionService>>());

        var result = await executionService.DebugNodeAsync(
            tenantId,
            workflowId,
            userId,
            new WorkflowV2NodeDebugRequest(
                "text_1",
                """
                {
                  "ticket": {
                    "payload": {
                      "summary": "主机异常登录"
                    }
                  }
                }
                """),
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.NotNull(result.OutputsJson);
        using var outputsDoc = JsonDocument.Parse(result.OutputsJson);
        Assert.Equal("告警：主机异常登录", outputsDoc.RootElement.GetProperty("rendered").GetString());

        Assert.Contains(capturedNodeExecutions, item => string.Equals(item.NodeKey, "text_1", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(capturedNodeExecutions, item => item.NodeKey.StartsWith("__debug_", StringComparison.OrdinalIgnoreCase));
    }
}
