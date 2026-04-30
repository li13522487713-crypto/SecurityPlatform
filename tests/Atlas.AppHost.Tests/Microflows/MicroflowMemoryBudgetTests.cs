using Atlas.Application.Microflows.Runtime;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowMemoryBudgetTests
{
    [Fact]
    public void Define_When_Value_Exceeds_Budget_Stores_ValueRef_Instead_Of_Raw()
    {
        var store = new MicroflowVariableStore();
        var budget = new ExecutionMemoryBudget
        {
            MaxVariableBytes = 32
        };
        var largeJson = $"\"{new string('x', 128)}\"";

        store.Define(new MicroflowVariableDefinition
        {
            Name = "payload",
            DataTypeJson = """{"kind":"string"}""",
            RawValueJson = largeJson,
            ValuePreview = "payload",
            ScopeKind = MicroflowVariableScopeKind.Action,
            MemoryBudget = budget,
            PreferReferenceWhenLarge = true,
            ValueRefKind = "blob"
        });

        var value = store.Get("payload");
        Assert.Null(value.RawValueJson);
        Assert.True(value.IsLargeObject);
        Assert.NotNull(value.ValueRef);
        Assert.True(value.EstimatedSizeBytes > budget.MaxVariableBytes);
    }
}
