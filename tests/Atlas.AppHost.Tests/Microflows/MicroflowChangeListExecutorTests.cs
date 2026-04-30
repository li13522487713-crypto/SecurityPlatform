using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowChangeListExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ChangeList_Add_Uses_Canonical_TargetListVariableName_And_ItemExpression()
    {
        var context = Context(new
        {
            targetListVariableName = "items",
            operation = "add",
            itemExpression = new { raw = "$itemToAdd" }
        });
        DefineList(context, "items", [1]);
        DefineScalar(context, "itemToAdd", 2, new { kind = "integer" });

        var result = await new ChangeListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([1, 2], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task ChangeList_AddAll_Uses_SourceListVariableName()
    {
        var context = Context(new
        {
            targetListVariableName = "items",
            sourceListVariableName = "incoming",
            operation = "addAll"
        });
        DefineList(context, "items", [1]);
        DefineList(context, "incoming", [2, 3]);

        var result = await new ChangeListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([1, 2, 3], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task ChangeList_RemoveWhere_Uses_ConditionExpression()
    {
        var context = Context(new
        {
            targetListVariableName = "items",
            operation = "removeWhere",
            objectVariableName = "item",
            conditionExpression = new { raw = "$item = 2" }
        });
        DefineList(context, "items", [1, 2, 3]);

        var result = await new ChangeListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal([1, 3], result.OutputJson!.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task ChangeList_Clear_Removes_All_Items()
    {
        var context = Context(new
        {
            targetListVariableName = "items",
            operation = "clear"
        });
        DefineList(context, "items", [1, 2, 3]);

        var result = await new ChangeListActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Empty(result.OutputJson!.Value.EnumerateArray());
    }

    private static MicroflowActionExecutionContext Context(object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "change-list-test", SchemaId = "change-list-test" };
        var runtime = RuntimeExecutionContext.Create(
            "run-change-list-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "change-list-node", ActionId = "change-list-action" },
            ObjectId = "change-list-node",
            ActionId = "change-list-action",
            ActionKind = "changeList",
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

    private static void DefineScalar<T>(MicroflowActionExecutionContext context, string name, T value, object dataType)
    {
        var json = JsonSerializer.SerializeToElement(value, JsonOptions);
        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = name,
            DataTypeJson = JsonSerializer.Serialize(dataType, JsonOptions),
            RawValueJson = json.GetRawText(),
            ValuePreview = json.GetRawText(),
            SourceKind = MicroflowVariableSourceKind.Parameter
        });
    }
}
