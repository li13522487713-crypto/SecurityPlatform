using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Branches;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class ParallelBranchIsolationContractTests
{
    [Fact]
    public void Detect_Returns_Conflict_When_Two_Branches_Write_Same_Variable()
    {
        var conflicts = GatewayWriteConflictDetector.Detect(
            [
                new BranchWriteIntent { BranchId = "a", VariableName = "payload" },
                new BranchWriteIntent { BranchId = "b", VariableName = "payload" }
            ]);

        Assert.Contains(conflicts, item => item.StartsWith(GatewayWriteConflictDetector.ParallelVariableWriteConflict, StringComparison.Ordinal));
    }

    [Fact]
    public void ScopeForker_Returns_Independent_Store_Instance()
    {
        IVariableScopeForker forker = new DefaultVariableScopeForker();
        var store = new MicroflowVariableStore();
        store.Define(new MicroflowVariableDefinition
        {
            Name = "value",
            DataTypeJson = """{"kind":"string"}""",
            RawValueJson = "\"root\"",
            ValuePreview = "root",
            ScopeKind = MicroflowVariableScopeKind.Global
        });

        var forked = forker.Fork(store, "branch-a");
        forked.Set("value", store.Get("value") with { RawValueJson = "\"branch\"", ValuePreview = "branch" });

        Assert.NotSame(store, forked);
        Assert.Equal("\"root\"", store.Get("value").RawValueJson);
        Assert.Equal("\"branch\"", forked.Get("value").RawValueJson);
    }
}
