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

    [Fact]
    public void DefaultBranchMergePolicy_Merges_Only_Written_Branch_Variables()
    {
        var target = new MicroflowVariableStore();
        target.Define(new MicroflowVariableDefinition
        {
            Name = "shared",
            DataTypeJson = """{"kind":"integer"}""",
            RawValueJson = "1",
            ValuePreview = "1",
            ScopeKind = MicroflowVariableScopeKind.Global
        });
        target.Define(new MicroflowVariableDefinition
        {
            Name = "readOnlyCopy",
            DataTypeJson = """{"kind":"string"}""",
            RawValueJson = "\"root\"",
            ValuePreview = "root",
            ScopeKind = MicroflowVariableScopeKind.Global
        });

        var branchStore = new MicroflowVariableStore();
        branchStore.Define(new MicroflowVariableDefinition
        {
            Name = "shared",
            DataTypeJson = """{"kind":"integer"}""",
            RawValueJson = "7",
            ValuePreview = "7",
            ScopeKind = MicroflowVariableScopeKind.ParallelBranch
        });
        branchStore.Define(new MicroflowVariableDefinition
        {
            Name = "readOnlyCopy",
            DataTypeJson = """{"kind":"string"}""",
            RawValueJson = "\"branch\"",
            ValuePreview = "branch",
            ScopeKind = MicroflowVariableScopeKind.ParallelBranch
        });
        branchStore.Define(new MicroflowVariableDefinition
        {
            Name = "branchOutput",
            DataTypeJson = """{"kind":"string"}""",
            RawValueJson = "\"created\"",
            ValuePreview = "created",
            ScopeKind = MicroflowVariableScopeKind.ParallelBranch
        });

        IBranchMergePolicy policy = new DefaultBranchMergePolicy();
        var result = policy.Merge(target, [
            new BranchExecutionContext
            {
                BranchId = "branch-a",
                VariableStore = branchStore,
                WrittenVariableNames = new HashSet<string>(["shared", "branchOutput"], StringComparer.Ordinal)
            }
        ]);

        Assert.Equal("7", target.Get("shared").RawValueJson);
        Assert.Equal("\"root\"", target.Get("readOnlyCopy").RawValueJson);
        Assert.Equal("\"created\"", target.Get("branchOutput").RawValueJson);
        Assert.Equal(MicroflowVariableScopeKind.BranchMerge, result.Variables.Single(item => item.VariableName == "branchOutput").Value.ScopeKind);
        Assert.DoesNotContain(result.Variables, item => item.VariableName == "readOnlyCopy");
    }
}
