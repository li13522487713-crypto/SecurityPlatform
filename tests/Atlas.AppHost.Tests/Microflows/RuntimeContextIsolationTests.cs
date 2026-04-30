using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class RuntimeContextIsolationTests
{
    [Fact]
    public void Contexts_Have_Isolated_NodeResults_Errors_And_CancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-context-isolation",
            SchemaId = "plan-context-isolation"
        };
        var first = RuntimeExecutionContext.Create(
            "run-1",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            cancellationToken: cts.Token);
        var second = RuntimeExecutionContext.Create(
            "run-2",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            cancellationToken: CancellationToken.None);

        first.NodeResults["start"] = new NodeExecutionResultSummary
        {
            ObjectId = "start",
            Status = "success"
        };
        first.Errors.Add(new MicroflowRuntimeErrorDto
        {
            Code = "RUNTIME_SAMPLE",
            Message = "sample error"
        });

        Assert.Single(first.NodeResults);
        Assert.Empty(second.NodeResults);
        Assert.Single(first.Errors);
        Assert.Empty(second.Errors);
        Assert.Equal(cts.Token, first.CancellationToken);
        Assert.Equal(CancellationToken.None, second.CancellationToken);
    }
}
