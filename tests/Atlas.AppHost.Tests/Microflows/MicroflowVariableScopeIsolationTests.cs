using Atlas.Application.Microflows.Runtime;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowVariableScopeIsolationTests
{
    [Fact]
    public void ParallelBranchScope_Shadowing_Does_Not_Leak_To_Outer_Scope()
    {
        var store = new MicroflowVariableStore();
        store.Define(new MicroflowVariableDefinition
        {
            Name = "value",
            DataTypeJson = """{"kind":"string"}""",
            RawValueJson = "\"outer\"",
            ValuePreview = "outer",
            ScopeKind = MicroflowVariableScopeKind.Global
        });

        using (store.PushScope(new MicroflowVariableScopeFrame
        {
            Kind = MicroflowVariableScopeKind.ParallelBranch,
            ObjectId = "branch-a"
        }))
        {
            store.Define(new MicroflowVariableDefinition
            {
                Name = "value",
                DataTypeJson = """{"kind":"string"}""",
                RawValueJson = "\"inner\"",
                ValuePreview = "inner",
                ScopeKind = MicroflowVariableScopeKind.ParallelBranch,
                AllowRedeclare = true
            });

            Assert.Equal("\"inner\"", store.Get("value").RawValueJson);
        }

        Assert.Equal("\"outer\"", store.Get("value").RawValueJson);
    }
}
