using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Loops;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowLoopControlActionExecutorsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Break_Allows_TargetLoopObjectId_In_Current_Loop_Stack()
    {
        var context = Context("break", new { targetLoopObjectId = "outer-loop" });
        using var outer = context.RuntimeExecutionContext.PushLoopScope("outer-loop", "outer-collection", "outerItem", 0, null, defineIterator: false);

        var result = await new BreakActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowLoopBodyExecutionStatus.Break, result.Status);
        Assert.Equal("outer-loop", result.TargetLoopObjectId);
    }

    [Fact]
    public async Task Continue_Fails_When_TargetLoopObjectId_Is_Out_Of_Scope()
    {
        var context = Context("continue", new { targetLoopObjectId = "missing-loop" });
        using var outer = context.RuntimeExecutionContext.PushLoopScope("outer-loop", "outer-collection", "outerItem", 0, null, defineIterator: false);

        var result = await new ContinueActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeLoopControlOutOfScope, result.Error?.Code);
    }

    private static MicroflowActionExecutionContext Context(string actionKind, object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "loop-control-action", SchemaId = "loop-control-action" };
        var runtime = RuntimeExecutionContext.Create(
            "run-loop-control-action",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "loop-control-node", ActionId = "loop-control-action" },
            ObjectId = "loop-control-node",
            ActionId = "loop-control-action",
            ActionKind = actionKind,
            ActionConfig = JsonSerializer.SerializeToElement(config, JsonOptions),
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry()
        };
    }
}
