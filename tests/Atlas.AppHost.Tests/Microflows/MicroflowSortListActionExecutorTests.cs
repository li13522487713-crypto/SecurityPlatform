using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowSortListActionExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SortList_Uses_SortExpression_With_Item_Scope()
    {
        var context = Context(new
        {
            sourceListVariableName = "items",
            outputVariableName = "ordered",
            sortExpression = new { raw = "$item/score" },
            direction = "asc"
        });
        DefineList(context, "items", [new { score = 2 }, new { score = 1 }, new { score = 3 }]);

        var result = await new SortListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([1, 2, 3], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetProperty("score").GetInt32()).ToArray());
    }

    [Fact]
    public async Task SortList_Uses_SortKeyExpression_With_Item_Scope()
    {
        var context = Context(new
        {
            sourceListVariableName = "items",
            outputVariableName = "ordered",
            sortKeys = new[]
            {
                new
                {
                    expression = new { raw = "$item/score" },
                    direction = "desc"
                }
            }
        });
        DefineList(context, "items", [new { score = 2 }, new { score = 1 }, new { score = 3 }]);

        var result = await new SortListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([3, 2, 1], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetProperty("score").GetInt32()).ToArray());
    }

    private static MicroflowActionExecutionContext Context(object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "sort-list-test", SchemaId = "sort-list-test" };
        var runtime = RuntimeExecutionContext.Create(
            "run-sort-list-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "sort-list-node", ActionId = "sort-list-action" },
            ObjectId = "sort-list-node",
            ActionId = "sort-list-action",
            ActionKind = "sortList",
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
