using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Loops;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowLoopExecutorControlTargetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Break_Targeting_Current_Loop_Exits_Current_Loop()
    {
        var (context, loopNode) = CreateContext(
            bodyExecutor: (_, _) => Task.FromResult(new MicroflowLoopBodyExecutionResult
            {
                Status = MicroflowLoopBodyExecutionStatus.Break
            }));

        var executor = CreateExecutor();
        var result = await executor.ExecuteLoopAsync(context, loopNode, CancellationToken.None);

        Assert.Equal(MicroflowLoopExecutionStatus.Break, result.Status);
        Assert.Equal("loop-node", result.TargetLoopObjectId);
    }

    [Fact]
    public async Task Break_Targeting_Ancestor_Loop_Propagates_Control_Signal()
    {
        var (context, loopNode) = CreateContext(
            bodyExecutor: (_, _) => Task.FromResult(new MicroflowLoopBodyExecutionResult
            {
                Status = MicroflowLoopBodyExecutionStatus.Break,
                TargetLoopObjectId = "outer-loop"
            }));

        using var outer = context.RuntimeExecutionContext.PushLoopScope("outer-loop", "outer-collection", "outerItem", 0, null, defineIterator: false);
        var executor = CreateExecutor();
        var result = await executor.ExecuteLoopAsync(context, loopNode, CancellationToken.None);

        Assert.Equal(MicroflowLoopExecutionStatus.Break, result.Status);
        Assert.Equal("outer-loop", result.TargetLoopObjectId);
    }

    [Fact]
    public async Task Continue_Targeting_Ancestor_Loop_Propagates_Control_Signal()
    {
        var (context, loopNode) = CreateContext(
            bodyExecutor: (_, _) => Task.FromResult(new MicroflowLoopBodyExecutionResult
            {
                Status = MicroflowLoopBodyExecutionStatus.Continue,
                TargetLoopObjectId = "outer-loop"
            }));

        using var outer = context.RuntimeExecutionContext.PushLoopScope("outer-loop", "outer-collection", "outerItem", 0, null, defineIterator: false);
        var executor = CreateExecutor();
        var result = await executor.ExecuteLoopAsync(context, loopNode, CancellationToken.None);

        Assert.Equal(MicroflowLoopExecutionStatus.Continue, result.Status);
        Assert.Equal("outer-loop", result.TargetLoopObjectId);
    }

    [Fact]
    public async Task Continue_Targeting_Current_Loop_Is_Consumed_By_Loop()
    {
        var iterationCount = 0;
        var (context, loopNode) = CreateContext(
            bodyExecutor: (_, _) =>
            {
                iterationCount++;
                return Task.FromResult(new MicroflowLoopBodyExecutionResult
                {
                    Status = MicroflowLoopBodyExecutionStatus.Continue
                });
            });

        var executor = CreateExecutor();
        var result = await executor.ExecuteLoopAsync(context, loopNode, CancellationToken.None);

        Assert.Equal(MicroflowLoopExecutionStatus.Success, result.Status);
        Assert.Equal(2, iterationCount);
    }

    private static MicroflowLoopExecutor CreateExecutor()
        => new(new MicroflowExpressionEvaluator(), new MicroflowActionExecutorRegistry(), new MicroflowTransactionManager(new SystemMicroflowClock()));

    private static (MicroflowActionExecutionContext Context, MicroflowExecutionNode LoopNode) CreateContext(
        Func<MicroflowLoopIterationContext, CancellationToken, Task<MicroflowLoopBodyExecutionResult>> bodyExecutor)
    {
        var loopNode = new MicroflowExecutionNode
        {
            ObjectId = "loop-node",
            Kind = "loopedActivity",
            CollectionId = "root-collection",
            ConfigJson = JsonSerializer.SerializeToElement(new
            {
                raw = new
                {
                    loopSource = new
                    {
                        kind = "iterableList",
                        listVariableName = "items",
                        iteratorVariableName = "item"
                    }
                }
            }, JsonOptions)
        };

        var plan = new MicroflowExecutionPlan
        {
            Id = "loop-plan",
            SchemaId = "loop-plan",
            Nodes =
            [
                loopNode,
                new MicroflowExecutionNode
                {
                    ObjectId = "body-node",
                    Kind = "actionActivity",
                    CollectionId = "loop-body",
                    RuntimeBehavior = "executable"
                }
            ],
            Flows = [],
            NormalFlows = [],
            LoopCollections =
            [
                new MicroflowExecutionLoopCollection
                {
                    LoopObjectId = "loop-node",
                    CollectionId = "loop-body",
                    Nodes = ["body-node"],
                    Flows = [],
                    StartLikeNodeIds = ["body-node"],
                    TerminalNodeIds = []
                }
            ]
        };

        var runtime = RuntimeExecutionContext.Create(
            "run-loop-executor",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);
        DefineList(runtime.VariableStore, "items", [1, 2]);

        return (new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = loopNode,
            ActionConfig = loopNode.ConfigJson!.Value,
            ActionKind = "loopedActivity",
            ObjectId = "loop-node",
            CollectionId = "root-collection",
            VariableStore = runtime.VariableStore,
            ExpressionEvaluator = new MicroflowExpressionEvaluator(),
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            LoopBodyExecutor = bodyExecutor
        }, loopNode);
    }

    private static void DefineList<T>(IMicroflowVariableStore store, string name, T[] items)
    {
        var json = JsonSerializer.SerializeToElement(items, JsonOptions);
        store.Define(new MicroflowVariableDefinition
        {
            Name = name,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "list" }, JsonOptions),
            RawValueJson = json.GetRawText(),
            ValuePreview = $"{name}[{items.Length}]",
            SourceKind = MicroflowVariableSourceKind.Parameter
        });
    }
}
