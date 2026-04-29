using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowListOperationSetExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnionDeduplicatesAndPreservesOrder()
    {
        var context = Context(new
        {
            operation = "union",
            inputListVariable = "left",
            secondListVariable = "right",
            outputVariable = "out"
        });
        DefineList(context, "left", [Obj("1"), Obj("2")]);
        DefineList(context, "right", [Obj("2"), Obj("3")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Equal(["1", "2", "3"], ReadIds(result.OutputJson!.Value));
        Assert.Equal(["1", "2"], ReadIds(ReadVariable(context, "left")));
    }

    [Fact]
    public async Task IntersectKeepsInputOrder()
    {
        var context = Context(new { operation = "intersect", inputListVariable = "left", secondListVariable = "right", outputVariable = "out" });
        DefineList(context, "left", [Obj("1"), Obj("2"), Obj("3")]);
        DefineList(context, "right", [Obj("3"), Obj("1")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(["1", "3"], ReadIds(result.OutputJson!.Value));
    }

    [Fact]
    public async Task SubtractRemovesSecondListItems()
    {
        var context = Context(new { operation = "subtract", inputListVariable = "left", secondListVariable = "right", outputVariable = "out" });
        DefineList(context, "left", [Obj("1"), Obj("2"), Obj("3")]);
        DefineList(context, "right", [Obj("2")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(["1", "3"], ReadIds(result.OutputJson!.Value));
    }

    [Fact]
    public async Task EqualsRequiresSameOrder()
    {
        var same = Context(new { operation = "equals", inputListVariable = "left", secondListVariable = "right", outputVariable = "same" });
        DefineList(same, "left", [Obj("1"), Obj("2")]);
        DefineList(same, "right", [Obj("1"), Obj("2")]);
        var sameResult = await new ListOperationActionExecutor().ExecuteAsync(same, CancellationToken.None);

        var different = Context(new { operation = "equals", inputListVariable = "left", secondListVariable = "right", outputVariable = "different" });
        DefineList(different, "left", [Obj("1"), Obj("2")]);
        DefineList(different, "right", [Obj("2"), Obj("1")]);
        var differentResult = await new ListOperationActionExecutor().ExecuteAsync(different, CancellationToken.None);

        Assert.True(sameResult.OutputJson!.Value.GetBoolean());
        Assert.False(differentResult.OutputJson!.Value.GetBoolean());
    }

    [Fact]
    public async Task DistinctRemovesDuplicates()
    {
        var context = Context(new { operation = "distinct", inputListVariable = "left", outputVariable = "out" });
        DefineList(context, "left", [Obj("1"), Obj("1"), Obj("2")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(["1", "2"], ReadIds(result.OutputJson!.Value));
    }

    private static MicroflowActionExecutionContext Context(object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "list-set-test", SchemaId = "list-set-test" };
        var runtime = RuntimeExecutionContext.Create(
            "run-list-set",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "list-node", ActionId = "list-action" },
            ObjectId = "list-node",
            ActionId = "list-action",
            ActionKind = "listOperation",
            ActionConfig = JsonSerializer.SerializeToElement(config, JsonOptions),
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry()
        };
    }

    private static void DefineList(MicroflowActionExecutionContext context, string name, object[] items)
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

    private static object Obj(string id)
        => new { id, entityType = "Sales.Order" };

    private static string[] ReadIds(JsonElement array)
        => array.EnumerateArray().Select(item => item.GetProperty("id").GetString()!).ToArray();

    private static JsonElement ReadVariable(MicroflowActionExecutionContext context, string name)
        => MicroflowVariableStore.ToJsonElement(context.VariableStore.Get(name).RawValueJson!)!.Value;
}
