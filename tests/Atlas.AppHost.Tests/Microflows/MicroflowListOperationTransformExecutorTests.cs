using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowListOperationTransformExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Filter_Uses_Canonical_ListOperation_Fields()
    {
        var context = Context(new
        {
            leftListVariableName = "items",
            outputListVariableName = "filtered",
            operation = "filter",
            objectVariableName = "item",
            filterExpression = new { raw = "$item > 1" }
        });
        DefineList(context, "items", [1, 2, 3]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([2, 3], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task Sort_Uses_SortKeys()
    {
        var context = Context(new
        {
            leftListVariableName = "items",
            outputListVariableName = "ordered",
            operation = "sort",
            sortKeys = new[] { new { field = "score", direction = "desc" } }
        });
        DefineList(context, "items", [new { score = 2 }, new { score = 1 }, new { score = 3 }]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([3, 2, 1], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetProperty("score").GetInt32()).ToArray());
    }

    [Fact]
    public async Task Map_Uses_Expression()
    {
        var context = Context(new
        {
            leftListVariableName = "items",
            outputListVariableName = "mapped",
            operation = "map",
            objectVariableName = "item",
            expression = new { raw = "$item * 2" }
        });
        DefineList(context, "items", [1, 2, 3]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([2, 4, 6], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task Take_And_Skip_Use_Limit_And_Offset()
    {
        var takeContext = Context(new
        {
            leftListVariableName = "items",
            outputListVariableName = "taken",
            operation = "take",
            limit = 2
        });
        DefineList(takeContext, "items", [1, 2, 3]);
        var takeResult = await new ListOperationActionExecutor().ExecuteAsync(takeContext, CancellationToken.None);

        var skipContext = Context(new
        {
            leftListVariableName = "items",
            outputListVariableName = "skipped",
            operation = "skip",
            offset = 1
        });
        DefineList(skipContext, "items", [1, 2, 3]);
        var skipResult = await new ListOperationActionExecutor().ExecuteAsync(skipContext, CancellationToken.None);

        Assert.Equal([1, 2], takeResult.OutputJson!.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray());
        Assert.Equal([2, 3], skipResult.OutputJson!.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    private static MicroflowActionExecutionContext Context(object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "list-transform-test", SchemaId = "list-transform-test" };
        var runtime = RuntimeExecutionContext.Create(
            "run-list-transform-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "list-operation-node", ActionId = "list-operation-action" },
            ObjectId = "list-operation-node",
            ActionId = "list-operation-action",
            ActionKind = "listOperation",
            ActionConfig = JsonSerializer.SerializeToElement(config, JsonOptions),
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            ExpressionEvaluator = new MicroflowExpressionEvaluator()
        };
    }

    private static void DefineList<T>(MicroflowActionExecutionContext context, string name, T[] items)
    {
        var json = JsonSerializer.SerializeToElement(items, JsonOptions);
        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = name,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "list" }, JsonOptions),
            RawValueJson = json.GetRawText(),
            ValuePreview = $"{name}[{items.Length}]",
            SourceKind = MicroflowVariableSourceKind.Parameter
        });
    }
}
