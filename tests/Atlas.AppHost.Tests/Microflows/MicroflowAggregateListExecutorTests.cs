using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowAggregateListExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("count", 3)]
    [InlineData("sum", 6)]
    [InlineData("average", 2)]
    [InlineData("min", 1)]
    [InlineData("max", 3)]
    public async Task AggregateList_Supports_All_AggregateFunctions(string aggregateFunction, int expected)
    {
        var context = Context(new
        {
            sourceListVariableName = "items",
            aggregateFunction,
            outputVariableName = "result",
            aggregateExpression = new { raw = "$item" },
            emptyListBehavior = "zero",
            resultType = aggregateFunction == "count"
                ? JsonSerializer.SerializeToElement(new { kind = "integer" }, JsonOptions)
                : JsonSerializer.SerializeToElement(new { kind = "decimal" }, JsonOptions)
        });
        DefineList(context, "items", [1, 2, 3]);

        var result = await new AggregateListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.NotNull(result.OutputJson);
        Assert.Equal(expected.ToString(), result.OutputJson!.Value.GetRawText().Trim('"'));
    }

    [Fact]
    public async Task AggregateList_Uses_AttributeQualifiedName_For_Object_Collections()
    {
        var context = Context(new
        {
            sourceListVariableName = "orders",
            aggregateFunction = "sum",
            attributeQualifiedName = "Amount",
            outputVariableName = "total",
            resultType = JsonSerializer.SerializeToElement(new { kind = "decimal" }, JsonOptions)
        });
        DefineList(context, "orders", [new { Amount = 2 }, new { Amount = 5 }]);

        var result = await new AggregateListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("7", result.OutputJson!.Value.GetRawText());
    }

    [Fact]
    public async Task AggregateList_EmptyListBehavior_Null_Returns_Null()
    {
        var context = Context(new
        {
            sourceListVariableName = "items",
            aggregateFunction = "sum",
            outputVariableName = "result",
            emptyListBehavior = "null"
        });
        DefineList(context, "items", Array.Empty<int>());

        var result = await new AggregateListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(JsonValueKind.Null, result.OutputJson!.Value.ValueKind);
    }

    [Fact]
    public async Task AggregateList_EmptyListBehavior_Error_Fails()
    {
        var context = Context(new
        {
            sourceListVariableName = "items",
            aggregateFunction = "sum",
            outputVariableName = "result",
            emptyListBehavior = "error"
        });
        DefineList(context, "items", Array.Empty<int>());

        var result = await new AggregateListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeVariableTypeMismatch, result.Error?.Code);
    }

    private static MicroflowActionExecutionContext Context(object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "aggregate-test", SchemaId = "aggregate-test" };
        var runtime = RuntimeExecutionContext.Create(
            "run-aggregate-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);

        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "aggregate-node", ActionId = "aggregate-action" },
            ObjectId = "aggregate-node",
            ActionId = "aggregate-action",
            ActionKind = "aggregateList",
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
