using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowListOperationScalarExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ContainsReturnsBoolean()
    {
        var context = Context(new { operation = "contains", inputListVariable = "items", item = Obj("2"), outputVariable = "containsResult" });
        DefineList(context, "items", [Obj("1"), Obj("2")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.OutputJson!.Value.GetBoolean());
        Assert.Contains("boolean", context.VariableStore.Get("containsResult").DataTypeJson);
    }

    [Fact]
    public async Task IsEmptyAndSizeReturnScalarValues()
    {
        var empty = Context(new { operation = "isEmpty", inputListVariable = "items", outputVariable = "emptyResult" });
        DefineList(empty, "items", []);
        var emptyResult = await new ListOperationActionExecutor().ExecuteAsync(empty, CancellationToken.None);

        var size = Context(new { operation = "size", inputListVariable = "items", outputVariable = "sizeResult" });
        DefineList(size, "items", [Obj("1"), Obj("2"), Obj("3")]);
        var sizeResult = await new ListOperationActionExecutor().ExecuteAsync(size, CancellationToken.None);

        Assert.True(emptyResult.OutputJson!.Value.GetBoolean());
        Assert.Equal(3, sizeResult.OutputJson!.Value.GetInt32());
    }

    [Theory]
    [InlineData("first", "1")]
    [InlineData("head", "1")]
    [InlineData("last", "3")]
    public async Task FirstHeadLastReturnExpectedObject(string operation, string expectedId)
    {
        var context = Context(new { operation, inputListVariable = "items", outputVariable = "out" });
        DefineList(context, "items", [Obj("1"), Obj("2"), Obj("3")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(expectedId, result.OutputJson!.Value.GetProperty("id").GetString());
    }

    [Fact]
    public async Task TailReturnsAllButFirst()
    {
        var context = Context(new { operation = "tail", inputListVariable = "items", outputVariable = "tailResult" });
        DefineList(context, "items", [Obj("1"), Obj("2"), Obj("3")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(["2", "3"], ReadIds(result.OutputJson!.Value));
    }

    [Fact]
    public async Task ReverseReturnsNewReversedListWithoutMutatingInput()
    {
        var context = Context(new { operation = "reverse", inputListVariable = "items", outputVariable = "reverseResult" });
        DefineList(context, "items", [Obj("1"), Obj("2"), Obj("3")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(["3", "2", "1"], ReadIds(result.OutputJson!.Value));
        Assert.Equal(["1", "2", "3"], ReadIds(ReadVariable(context, "items")));
    }

    [Fact]
    public async Task FindReturnsMatchingItemByValue()
    {
        var context = Context(new { operation = "find", inputListVariable = "items", item = Obj("2"), outputVariable = "found" });
        DefineList(context, "items", [Obj("1"), Obj("2"), Obj("3")]);

        var result = await new ListOperationActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("2", result.OutputJson!.Value.GetProperty("id").GetString());
    }

    private static MicroflowActionExecutionContext Context(object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "list-scalar-test", SchemaId = "list-scalar-test" };
        var runtime = RuntimeExecutionContext.Create(
            "run-list-scalar",
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
